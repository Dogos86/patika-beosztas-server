const env = process.env;

if (env.VITE_APP_ENV === "pilot") {
  const required = {
    VITE_DATA_SOURCE: "api",
    VITE_ENABLE_DEMO_LOGIN: "false",
    VITE_ENABLE_AI: "false",
    VITE_ENABLE_NOTIFICATIONS: "false",
  };

  for (const [name, expected] of Object.entries(required)) {
    if (env[name] !== expected) {
      throw new Error(`Pilot buildben ${name}=${expected} kötelező.`);
    }
  }

  if ((env.VITE_API_URL ?? "") !== "") {
    throw new Error(
      "Pilot buildben a VITE_API_URL legyen üres; a böngésző relatív /api útvonalat használ.",
    );
  }
}
