// Service locator. A komponensek CSAK innen érik el a szolgáltatásokat.
// A közös pilotkiadás API-only: a mock implementáció nincs importálva, ezért
// a demo rekordok és hitelesítő adatok nem kerülnek a production bundle-be.
import { httpServices } from "./http";
import type { Services } from "./interfaces";
import { frontendFeatures } from "@/config/features";

// A "phase1-http" átmeneti alias az api-hoz, kizárólag visszafelé kompatibilitásból.
const source = (import.meta.env.VITE_DATA_SOURCE ?? "api").toLowerCase();
const isApi = source === "api" || source === "phase1-http";

if (!isApi) {
  throw new Error(
    frontendFeatures.isPilot
      ? "Pilot módban kizárólag a valódi API adatforrás engedélyezett."
      : "Ez a kiadás API-only; a mock adatforrás nincs a bundle-ben.",
  );
}

export const services: Services = httpServices;
export const dataSource = "api" as const;

export type { Services } from "./interfaces";
