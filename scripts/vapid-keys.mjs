// Prints a new VAPID key pair (P-256, base64url) for web push reminders (docs/05 §10).
// Public key = uncompressed point (65 bytes), private key = scalar (32 bytes). Store both with user-secrets or
// environment variables, never in the repository:
//   node scripts/vapid-keys.mjs
//   dotnet user-secrets set "WebPush:PublicKey" "<publicKey>" --project src/Nomax.Api
//   dotnet user-secrets set "WebPush:PrivateKey" "<privateKey>" --project src/Nomax.Api
import { generateKeyPairSync } from "node:crypto";

const { publicKey, privateKey } = generateKeyPairSync("ec", { namedCurve: "prime256v1" });
const pub = publicKey.export({ format: "jwk" });
const priv = privateKey.export({ format: "jwk" });
const point = Buffer.concat([Buffer.from([4]), Buffer.from(pub.x, "base64url"), Buffer.from(pub.y, "base64url")]);

console.log(JSON.stringify({ publicKey: point.toString("base64url"), privateKey: priv.d }, null, 2));
