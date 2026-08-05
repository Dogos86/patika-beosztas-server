function firstHeaderValue(value) {
  if (Array.isArray(value)) return value[0];
  return value?.split(",")[0]?.trim();
}

export function buildUpstreamHeaders(requestHeaders, target, isPilot, remoteAddress) {
  const headers = { ...requestHeaders };

  // A session- és antiforgery-cookie-t változtatás nélkül adjuk tovább az
  // API-nak. A kifejezett hozzárendelés megakadályozza, hogy egy későbbi
  // gateway-header szűrés véletlenül elhagyja.
  if (requestHeaders.cookie !== undefined) {
    headers.cookie = requestHeaders.cookie;
  }

  headers.host = target.host;
  headers["x-forwarded-host"] = firstHeaderValue(requestHeaders.host) ?? "";
  headers["x-forwarded-proto"] =
    firstHeaderValue(requestHeaders["x-forwarded-proto"]) ?? (isPilot ? "https" : "http");
  headers["x-forwarded-for"] =
    firstHeaderValue(requestHeaders["x-forwarded-for"]) ?? remoteAddress ?? "127.0.0.1";
  return headers;
}

export function buildDownstreamHeaders(upstreamHeaders, pathname) {
  const headers = { ...upstreamHeaders };

  // A Node set-cookie tömbjét megőrizzük, így több cookie nem lapul össze
  // egyetlen, böngésző által hibásan értelmezett fejlécbe.
  if (upstreamHeaders["set-cookie"] !== undefined) {
    headers["set-cookie"] = upstreamHeaders["set-cookie"];
  }
  if (pathname === "/api/auth/csrf") {
    headers["cache-control"] = "no-store";
    headers.pragma = "no-cache";
    headers.expires = "0";
  }
  return headers;
}
