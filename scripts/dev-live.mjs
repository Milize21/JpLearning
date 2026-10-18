#!/usr/bin/env node
// Live local environment for development: keeps the Docker infrastructure, API, Gateway and Vite running,
// brings back anything that goes down, and rebuilds + restarts the .NET apps when their source changes.
// The .NET apps run from copies under .live/run, so `dotnet build` / `dotnet test` in the repo never hit
// files locked by a running app, and a failed rebuild leaves the previous instance serving.
//
//   node scripts/dev-live.mjs start              start in the background (logs in .live/logs)
//   node scripts/dev-live.mjs run                run in the foreground (Ctrl+C stops everything)
//   node scripts/dev-live.mjs status             show what is running and healthy
//   node scripts/dev-live.mjs restart <service>  restart api | gateway | web
//   node scripts/dev-live.mjs stop               stop the supervisor and the apps (Docker keeps running)
import { spawn, spawnSync } from "node:child_process";
import fs from "node:fs";
import net from "node:net";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const liveDir = path.join(root, ".live");
const logDir = path.join(liveDir, "logs");
const runRoot = path.join(liveDir, "run");
const artifactsDir = path.join(liveDir, "artifacts");
const statePath = path.join(liveDir, "state.json");
const composeFile = path.join(root, "infra", "docker-compose.yml");
const isWindows = process.platform === "win32";

const envFile = readEnvFile(path.join(root, "infra", ".env"));
const infraPorts = {
  postgres: Number(envFile.POSTGRES_PORT ?? 5432),
  valkey: Number(envFile.VALKEY_PORT ?? 6379),
  mailpit: 8025,
};

const services = {
  api: {
    kind: "dotnet",
    project: "src/Nomax.Api",
    assembly: "Nomax.Api",
    url: "http://localhost:5080",
    health: "/health/live",
    needsInfra: true,
  },
  gateway: {
    kind: "dotnet",
    project: "src/Nomax.Gateway",
    assembly: "Nomax.Gateway",
    url: "http://localhost:8080",
    health: "/health/live",
    needsInfra: false,
  },
  web: {
    kind: "node",
    cwd: "web",
    script: "web/node_modules/vite/bin/vite.js",
    url: "http://localhost:5173",
    health: "/",
    needsInfra: false,
  },
};
const order = ["api", "gateway", "web"];

const startGraceMs = 90_000; // a cold .NET start runs migrations and seeds
const healthFailuresBeforeRestart = 6; // x 5 s
const restartWindowMs = 180_000;
const restartsPerWindow = 5;

function readEnvFile(file) {
  const values = {};
  if (!fs.existsSync(file)) return values;
  for (const raw of fs.readFileSync(file, "utf8").split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith("#") || !line.includes("=")) continue;
    const index = line.indexOf("=");
    values[line.slice(0, index)] = line.slice(index + 1);
  }
  return values;
}

function readState() {
  try {
    return JSON.parse(fs.readFileSync(statePath, "utf8"));
  } catch {
    return null;
  }
}

function isAlive(pid) {
  if (!pid) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === "EPERM";
  }
}

function killTree(pid) {
  if (!pid) return;
  if (isWindows) {
    spawnSync("taskkill", ["/pid", String(pid), "/T", "/F"], { stdio: "ignore", windowsHide: true });
  } else {
    try {
      process.kill(-pid, "SIGTERM");
    } catch {
      try {
        process.kill(pid, "SIGTERM");
      } catch {
        // already gone
      }
    }
  }
}

/** Guards against PID reuse: only dotnet/node processes can be apps this supervisor started. */
function isOwnApp(pid) {
  if (!isAlive(pid)) return false;
  const result = isWindows
    ? spawnSync("tasklist", ["/FI", `PID eq ${pid}`, "/FO", "CSV", "/NH"], { encoding: "utf8", windowsHide: true })
    : spawnSync("ps", ["-p", String(pid), "-o", "comm="], { encoding: "utf8" });
  return /\b(dotnet|node)(\.exe)?\b/i.test(result.stdout ?? "");
}

async function httpOk(url) {
  try {
    const response = await fetch(url, { signal: AbortSignal.timeout(3000), redirect: "manual" });
    return response.status < 500;
  } catch {
    return false;
  }
}

function tcpOpen(port) {
  return new Promise((resolve) => {
    const socket = net.connect({ host: "127.0.0.1", port });
    const done = (ok) => {
      socket.destroy();
      resolve(ok);
    };
    socket.setTimeout(2000, () => done(false));
    socket.once("connect", () => done(true));
    socket.once("error", () => done(false));
  });
}

// ---------------------------------------------------------------- supervisor ----

const runtime = Object.fromEntries(
  order.map((name) => [
    name,
    {
      child: null,
      startedAt: 0,
      failures: 0,
      restarts: [],
      runDir: null,
      external: false,
      healthy: false,
      lastBuild: null,
      deploying: false,
    },
  ]),
);
const infra = { healthy: false, failures: 0, lastComposeUp: 0, lastDockerLaunch: 0, waitingLogged: false };
let stopping = false;

function log(message) {
  const line = `${new Date().toISOString()} ${message}`;
  console.log(line);
  fs.appendFileSync(path.join(logDir, "supervisor.log"), `${line}\n`);
}

function writeState() {
  const state = {
    supervisorPid: process.pid,
    updatedAt: new Date().toISOString(),
    infraHealthy: infra.healthy,
    services: Object.fromEntries(
      order.map((name) => {
        const rt = runtime[name];
        return [
          name,
          {
            url: services[name].url,
            pid: rt.child?.pid ?? null,
            healthy: rt.healthy,
            external: rt.external,
            startedAt: rt.startedAt ? new Date(rt.startedAt).toISOString() : null,
            lastBuild: rt.lastBuild,
          },
        ];
      }),
    ),
  };
  fs.writeFileSync(statePath, JSON.stringify(state, null, 2));
}

function openLog(name) {
  const file = path.join(logDir, `${name}.log`);
  try {
    if (fs.statSync(file).size > 20 * 1024 * 1024) fs.renameSync(file, `${file}.old`);
  } catch {
    // no log yet
  }
  return fs.openSync(file, "a");
}

function launch(name) {
  const svc = services[name];
  const rt = runtime[name];
  let command;
  let args;
  let cwd;
  let extraEnv;
  if (svc.kind === "dotnet") {
    command = "dotnet";
    args = [path.join(rt.runDir, `${svc.assembly}.dll`)];
    // The project folder is the content root, exactly like `dotnet run` (appsettings, storage paths).
    cwd = path.join(root, svc.project);
    extraEnv = { ASPNETCORE_ENVIRONMENT: "Development", ASPNETCORE_URLS: svc.url, DOTNET_NOLOGO: "1" };
  } else {
    command = process.execPath;
    args = [path.join(root, svc.script)];
    cwd = path.join(root, svc.cwd);
    extraEnv = { NO_COLOR: "1", FORCE_COLOR: "0" };
  }
  const out = openLog(name);
  fs.writeSync(out, `\n===== ${new Date().toISOString()} start ${name} =====\n`);
  const child = spawn(command, args, {
    cwd,
    env: { ...process.env, ...extraEnv },
    stdio: ["ignore", out, out],
    windowsHide: true,
    detached: !isWindows,
  });
  fs.closeSync(out);
  rt.child = child;
  rt.startedAt = Date.now();
  rt.failures = 0;
  rt.healthy = false;
  rt.restarts.push(Date.now());
  log(`${name}: started (pid ${child.pid})`);
  child.once("exit", (code, signal) => {
    if (rt.child === child) rt.child = null;
    rt.healthy = false;
    if (!stopping) log(`${name}: exited (code ${code ?? "-"}${signal ? `, ${signal}` : ""}); see .live/logs/${name}.log`);
  });
}

async function stopChild(name) {
  const child = runtime[name].child;
  if (!child) return;
  runtime[name].child = null;
  const exited = new Promise((resolve) => {
    if (child.exitCode !== null) resolve();
    else child.once("exit", resolve);
  });
  killTree(child.pid);
  await Promise.race([exited, new Promise((resolve) => setTimeout(resolve, 10_000))]);
}

function runCommand(command, args, logName) {
  return new Promise((resolve) => {
    const out = openLog(logName);
    fs.writeSync(out, `\n===== ${new Date().toISOString()} ${command} ${args.join(" ")} =====\n`);
    const child = spawn(command, args, { cwd: root, stdio: ["ignore", out, out], windowsHide: true });
    fs.closeSync(out);
    child.once("error", () => resolve(-1));
    child.once("exit", (code) => resolve(code ?? -1));
  });
}

async function build(name) {
  const svc = services[name];
  const started = Date.now();
  log(`${name}: building`);
  const code = await runCommand(
    "dotnet",
    [
      "build",
      path.join(root, svc.project),
      "-c",
      "Debug",
      "--artifacts-path",
      artifactsDir,
      "-nologo",
      "-v",
      "q",
      "-clp:ErrorsOnly",
    ],
    "build",
  );
  const seconds = Math.round((Date.now() - started) / 1000);
  runtime[name].lastBuild = { at: new Date().toISOString(), ok: code === 0, seconds };
  if (code !== 0) {
    log(`${name}: build FAILED after ${seconds}s, the running instance stays up (see .live/logs/build.log)`);
    return null;
  }
  const output = path.join(artifactsDir, "bin", svc.assembly, "debug");
  const runDir = path.join(runRoot, `${name}-${Date.now()}`);
  fs.cpSync(output, runDir, { recursive: true });
  log(`${name}: build ok in ${seconds}s`);
  return runDir;
}

async function deploy(name, runDir) {
  const rt = runtime[name];
  const previous = rt.runDir;
  // The health loop must not restart the old build while it is being replaced.
  rt.deploying = true;
  try {
    rt.runDir = runDir;
    await stopChild(name);
    rt.restarts = [];
    if (!rt.external) launch(name);
  } finally {
    rt.deploying = false;
  }
  if (previous && previous !== runDir) removeLater(previous);
}

function removeLater(dir) {
  setTimeout(() => fs.rm(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 1000 }, () => {}), 5000);
}

// Source watching: changes are batched, built one service at a time, and deployed only when the build succeeds.
const pendingBuilds = new Set();
let building = false;
let buildTimer = null;

function markChanged(...names) {
  for (const name of names) pendingBuilds.add(name);
  clearTimeout(buildTimer);
  buildTimer = setTimeout(runBuilds, 2500);
}

async function runBuilds() {
  if (building || stopping) return;
  building = true;
  try {
    while (pendingBuilds.size > 0 && !stopping) {
      const batch = order.filter((name) => pendingBuilds.has(name));
      pendingBuilds.clear();
      for (const name of batch) {
        const runDir = await build(name);
        if (runDir && !stopping) await deploy(name, runDir);
      }
    }
  } finally {
    building = false;
  }
}

function watchSources() {
  const extensions = new Set([".cs", ".csproj", ".props", ".targets", ".json", ".resx"]);
  fs.watch(path.join(root, "src"), { recursive: true }, (_event, file) => {
    if (!file) return;
    const rel = file.replaceAll("\\", "/");
    if (/(^|\/)(bin|obj)\//.test(rel) || !extensions.has(path.extname(rel))) return;
    if (rel.startsWith("Nomax.Gateway/")) markChanged("gateway");
    else if (rel.startsWith("BuildingBlocks/")) markChanged("api", "gateway");
    else markChanged("api");
  });
  fs.watch(root, (_event, file) => {
    if (file === "Directory.Build.props" || file === "Directory.Packages.props") markChanged("api", "gateway");
  });
}

// Infrastructure: Docker Compose services must answer; if not, start Docker Desktop (Windows) and `compose up`.
async function checkInfra() {
  const checks = await Promise.all([
    tcpOpen(infraPorts.postgres),
    tcpOpen(infraPorts.valkey),
    tcpOpen(infraPorts.mailpit),
    httpOk("http://localhost:8081/realms/nomax"),
  ]);
  const healthy = checks.every(Boolean);
  if (healthy) {
    if (!infra.healthy) log("infra: healthy (postgres, valkey, keycloak, mailpit)");
    infra.healthy = true;
    infra.failures = 0;
    return;
  }
  infra.healthy = false;
  infra.failures += 1;
  if (infra.failures < 2 || Date.now() - infra.lastComposeUp < 90_000) return;
  infra.lastComposeUp = Date.now();
  const docker = spawnSync("docker", ["info", "--format", "{{.ServerVersion}}"], { timeout: 20_000, windowsHide: true });
  if (docker.status !== 0) {
    const desktop = "C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe";
    if (isWindows && fs.existsSync(desktop) && Date.now() - infra.lastDockerLaunch > 180_000) {
      infra.lastDockerLaunch = Date.now();
      log("infra: Docker is not running, starting Docker Desktop");
      spawn(desktop, [], { detached: true, stdio: "ignore" }).unref();
    } else {
      log("infra: Docker is not running yet");
    }
    return;
  }
  log("infra: services down, running docker compose up -d");
  const code = await runCommand("docker", ["compose", "-f", composeFile, "up", "-d"], "infra");
  log(`infra: docker compose up -d finished (code ${code})`);
}

async function checkService(name) {
  const svc = services[name];
  const rt = runtime[name];
  const url = `${svc.url}${svc.health}`;
  if (rt.deploying) return;
  if (rt.child) {
    if (Date.now() - rt.startedAt < 3000) return;
    const ok = await httpOk(url);
    if (ok) {
      if (!rt.healthy) log(`${name}: healthy at ${svc.url}`);
      rt.healthy = true;
      rt.failures = 0;
      return;
    }
    rt.healthy = false;
    if (Date.now() - rt.startedAt < startGraceMs) return;
    rt.failures += 1;
    if (rt.failures >= healthFailuresBeforeRestart) {
      log(`${name}: not answering at ${url}, restarting`);
      await stopChild(name);
    }
    return;
  }
  if (svc.kind === "dotnet" && !rt.runDir) return; // first build still running
  // Something else (e.g. a manual `dotnet run`) already serves this port: leave it alone.
  if (await httpOk(url)) {
    if (!rt.external) log(`${name}: already served by another process at ${svc.url}, not starting a second one`);
    rt.external = true;
    rt.healthy = true;
    return;
  }
  if (rt.external) log(`${name}: external process is gone, taking over`);
  rt.external = false;
  rt.healthy = false;
  if (svc.needsInfra && !infra.healthy) {
    if (!infra.waitingLogged) log(`${name}: waiting for infrastructure`);
    infra.waitingLogged = true;
    return;
  }
  rt.restarts = rt.restarts.filter((at) => Date.now() - at < restartWindowMs);
  const last = rt.restarts.at(-1) ?? 0;
  if (rt.restarts.length >= restartsPerWindow && Date.now() - last < 60_000) return; // crash loop: back off
  launch(name);
}

async function supervise() {
  fs.mkdirSync(logDir, { recursive: true });
  const existing = readState();
  if (existing && existing.supervisorPid !== process.pid && isAlive(existing.supervisorPid)) {
    console.log(`dev-live is already running (pid ${existing.supervisorPid}). Use "status" or "stop".`);
    process.exit(1);
  }
  // A supervisor that died abruptly leaves its apps behind; stop them so they get rebuilt and watched again.
  for (const info of Object.values(existing?.services ?? {})) {
    if (info.pid && isOwnApp(info.pid)) killTree(info.pid);
  }
  fs.rmSync(runRoot, { recursive: true, force: true, maxRetries: 3 });
  fs.mkdirSync(runRoot, { recursive: true });
  log(`supervisor: started (pid ${process.pid}) in ${root}`);
  writeState();

  const shutdown = async () => {
    if (stopping) return;
    stopping = true;
    log("supervisor: stopping apps");
    await Promise.all(order.map((name) => stopChild(name)));
    fs.rmSync(statePath, { force: true });
    process.exit(0);
  };
  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
  process.on("SIGHUP", shutdown);

  await checkInfra();
  watchSources();
  // Initial build; the web dev server does not need one.
  markChanged("api", "gateway");
  clearTimeout(buildTimer);
  void runBuilds();

  for (;;) {
    if (stopping) return;
    try {
      await checkInfra();
      for (const name of order) await checkService(name);
      writeState();
    } catch (error) {
      log(`supervisor: check failed: ${error instanceof Error ? error.message : String(error)}`);
    }
    await new Promise((resolve) => setTimeout(resolve, 5000));
  }
}

// ------------------------------------------------------------------ commands ----

async function status() {
  const state = readState();
  const running = state && isAlive(state.supervisorPid);
  console.log(running ? `supervisor: running (pid ${state.supervisorPid})` : "supervisor: not running");
  for (const name of order) {
    const svc = services[name];
    const ok = await httpOk(`${svc.url}${svc.health}`);
    const info = state?.services?.[name];
    const build = info?.lastBuild ? ` · last build ${info.lastBuild.ok ? "ok" : "FAILED"} ${info.lastBuild.at}` : "";
    console.log(`${name.padEnd(8)} ${ok ? "UP  " : "DOWN"} ${svc.url}${info?.pid ? ` (pid ${info.pid})` : ""}${build}`);
  }
  const infraChecks = await Promise.all([
    tcpOpen(infraPorts.postgres),
    tcpOpen(infraPorts.valkey),
    httpOk("http://localhost:8081/realms/nomax"),
    tcpOpen(infraPorts.mailpit),
  ]);
  console.log(
    `infra    postgres:${infraChecks[0] ? "UP" : "DOWN"} valkey:${infraChecks[1] ? "UP" : "DOWN"} ` +
      `keycloak:${infraChecks[2] ? "UP" : "DOWN"} mailpit:${infraChecks[3] ? "UP" : "DOWN"}`,
  );
  console.log(`logs     ${logDir}`);
}

function start() {
  const state = readState();
  if (state && isAlive(state.supervisorPid)) {
    console.log(`dev-live is already running (pid ${state.supervisorPid}).`);
    return;
  }
  fs.mkdirSync(logDir, { recursive: true });
  const child = spawn(process.execPath, [fileURLToPath(import.meta.url), "run"], {
    cwd: root,
    detached: true,
    stdio: "ignore",
    windowsHide: true,
  });
  child.unref();
  console.log(`dev-live started in the background (pid ${child.pid}). Logs: ${logDir}`);
  console.log("Open http://localhost:5173 once the first build finishes (about a minute).");
}

function stop() {
  const state = readState();
  if (!state || !isAlive(state.supervisorPid)) {
    console.log("dev-live is not running.");
    return;
  }
  for (const info of Object.values(state.services ?? {})) killTree(info.pid);
  killTree(state.supervisorPid);
  fs.rmSync(statePath, { force: true });
  console.log("dev-live stopped. Docker infrastructure keeps running (docker compose -f infra/docker-compose.yml stop).");
}

function restart(name) {
  const state = readState();
  const info = state?.services?.[name];
  if (!state || !isAlive(state.supervisorPid) || !info) {
    console.log(`Unknown service or dev-live not running. Services: ${order.join(", ")}`);
    return;
  }
  killTree(info.pid);
  console.log(`${name} stopped; the supervisor starts it again within a few seconds.`);
}

const [command = "status", argument] = process.argv.slice(2);
switch (command) {
  case "run":
    await supervise();
    break;
  case "start":
    start();
    break;
  case "stop":
    stop();
    break;
  case "restart":
    restart(argument);
    break;
  case "status":
    await status();
    break;
  default:
    console.log("Usage: node scripts/dev-live.mjs <start|run|status|restart <service>|stop>");
    process.exitCode = 1;
}
