// @lovable.dev/vite-tanstack-config already includes the following — do NOT add them manually
// or the app will break with duplicate plugins:
//   - TanStack devtools (dev-only, first), tanstackStart, viteReact, tailwindcss, tsConfigPaths,
//     nitro (build-only using cloudflare as a default target), VITE_* env injection, @ path alias,
//     React/TanStack dedupe, error logger plugins, and sandbox detection (port/host/strictPort).
// You can pass additional config via defineConfig({ vite: { ... }, etc... }) if needed.
import { defineConfig } from "@lovable.dev/vite-tanstack-config";

// Lokális fejlesztéshez a backend (ASP.NET Core, cookie + CSRF) azonos site alatt fut.
// Az env változóból olvasott VITE_API_PROXY_TARGET felé továbbítjuk az /api, /health és
// /openapi útvonalakat, hogy a böngésző csak a frontend originhez kapcsolódjon.
const proxyTarget = process.env.VITE_API_PROXY_TARGET;
const proxyPaths = ["/api", "/health", "/openapi"];
const proxy = proxyTarget
  ? Object.fromEntries(
      proxyPaths.map((p) => [
        p,
        {
          target: proxyTarget,
          changeOrigin: true,
          secure: false,
          ws: true,
        },
      ]),
    )
  : undefined;

export default defineConfig({
  tanstackStart: {
    // Redirect TanStack Start's bundled server entry to src/server.ts (our SSR error wrapper).
    // nitro/vite builds from this
    server: { entry: "server" },
  },
  nitro: {
    preset: "node-server",
  },
  vite: proxy ? { server: { proxy } } : undefined,
});
