import { fileURLToPath, URL } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// The SPA talks to the Gateway only (docs/02 §13); in development Vite proxies to it on port 8080.
const gateway = process.env.NOMAX_GATEWAY_URL ?? "http://localhost:8080";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": gateway,
      "/hubs": { target: gateway, ws: true },
      "/ical": gateway,
      "/openapi": gateway,
    },
  },
  build: {
    sourcemap: true,
  },
});
