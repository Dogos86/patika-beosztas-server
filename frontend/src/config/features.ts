type FrontendEnvironment = Record<string, string | boolean | undefined>;

export type FrontendFeatures = {
  isPilot: boolean;
  demoLoginEnabled: boolean;
  aiEnabled: boolean;
  notificationsEnabled: boolean;
};

function enabled(value: string | boolean | undefined): boolean {
  return String(value).toLowerCase() === "true";
}

export function resolveFrontendFeatures(env: FrontendEnvironment): FrontendFeatures {
  const isPilot = env.VITE_APP_ENV === "pilot";
  const dataSource = String(env.VITE_DATA_SOURCE ?? "mock").toLowerCase();
  const features = {
    isPilot,
    demoLoginEnabled: enabled(env.VITE_ENABLE_DEMO_LOGIN),
    aiEnabled: enabled(env.VITE_ENABLE_AI),
    notificationsEnabled: enabled(env.VITE_ENABLE_NOTIFICATIONS),
  };

  if (
    isPilot &&
    (dataSource !== "api" ||
      features.demoLoginEnabled ||
      features.aiEnabled ||
      features.notificationsEnabled)
  ) {
    throw new Error(
      "Hibás pilot konfiguráció: valódi API és kikapcsolt demo/AI/értesítések kötelezők.",
    );
  }

  return features;
}

export const frontendFeatures = resolveFrontendFeatures(import.meta.env);
