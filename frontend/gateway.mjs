import http from "node:http";
import { spawn } from "node:child_process";

const publicPort = Number.parseInt(process.env.PORT ?? "3000", 10);
const frontendPort = Number.parseInt(process.env.FRONTEND_PORT ?? "3001", 10);
const apiInternalUrl = process.env.API_INTERNAL_URL;
const isPilot = process.env.VITE_APP_ENV === "pilot";

if (!Number.isInteger(publicPort) || publicPort < 1 || publicPort > 65535) {
  throw new Error("A PORT érvénytelen.");
}

if (!Number.isInteger(frontendPort) || frontendPort < 1 || frontendPort > 65535) {
  throw new Error("A FRONTEND_PORT érvénytelen.");
}

if (!apiInternalUrl) {
  throw new Error("Az API_INTERNAL_URL kötelező.");
}

const apiTarget = new URL(apiInternalUrl);
if (apiTarget.protocol !== "http:") {
  throw new Error("Az API_INTERNAL_URL a Railway private network HTTP-címe legyen.");
}

if (isPilot && !apiTarget.hostname.endsWith(".railway.internal")) {
  throw new Error("Pilot módban az API_INTERNAL_URL Railway private network cím legyen.");
}

const frontendTarget = new URL(`http://127.0.0.1:${frontendPort}`);
const frontend = spawn(process.execPath, ["/app/frontend/server/index.mjs"], {
  stdio: "inherit",
  env: {
    ...process.env,
    HOST: "127.0.0.1",
    PORT: String(frontendPort),
  },
});

frontend.once("exit", (code, signal) => {
  console.error(`A frontend folyamat leállt (code=${code}, signal=${signal}).`);
  process.exit(code ?? 1);
});

function isApiPath(pathname) {
  return ["/api", "/health", "/openapi"].some(
    (prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
  );
}

function firstHeaderValue(value) {
  if (Array.isArray(value)) return value[0];
  return value?.split(",")[0]?.trim();
}

const server = http.createServer((request, response) => {
  const incomingUrl = new URL(request.url ?? "/", "http://gateway.local");
  const target = isApiPath(incomingUrl.pathname) ? apiTarget : frontendTarget;
  const headers = { ...request.headers };

  headers.host = target.host;
  headers["x-forwarded-host"] = firstHeaderValue(request.headers.host) ?? "";
  headers["x-forwarded-proto"] =
    firstHeaderValue(request.headers["x-forwarded-proto"]) ?? (isPilot ? "https" : "http");
  headers["x-forwarded-for"] =
    firstHeaderValue(request.headers["x-forwarded-for"]) ??
    request.socket.remoteAddress ??
    "127.0.0.1";

  const upstream = http.request(
    {
      protocol: target.protocol,
      hostname: target.hostname,
      port: target.port,
      method: request.method,
      path: `${incomingUrl.pathname}${incomingUrl.search}`,
      headers,
    },
    (upstreamResponse) => {
      response.writeHead(
        upstreamResponse.statusCode ?? 502,
        upstreamResponse.statusMessage,
        upstreamResponse.headers,
      );
      upstreamResponse.pipe(response);
    },
  );

  upstream.on("error", (error) => {
    console.error(`Gateway upstream hiba (${target.host}):`, error.message);
    if (!response.headersSent) {
      response.writeHead(502, { "content-type": "text/plain; charset=utf-8" });
    }
    response.end("A szolgáltatás átmenetileg nem érhető el.");
  });

  request.pipe(upstream);
});

server.listen(publicPort, "0.0.0.0", () => {
  console.log(`A web gateway a 0.0.0.0:${publicPort} címen figyel.`);
});

function shutdown(signal) {
  console.log(`${signal}: a web gateway leáll.`);
  server.close(() => {
    frontend.kill(signal);
  });
  setTimeout(() => process.exit(1), 10_000).unref();
}

process.once("SIGTERM", () => shutdown("SIGTERM"));
process.once("SIGINT", () => shutdown("SIGINT"));
