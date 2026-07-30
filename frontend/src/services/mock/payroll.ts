import type { PayrollService } from "../interfaces";
import type {
  EmployeePayrollProfile,
  PayrollOnboardingSummary,
  TaxAllowanceSurvey,
  TaxAllowanceSurveyAnswers,
  TaxDeclarationRequirement,
  User,
} from "../types";
import { maskTaxId } from "@/lib/mask";
import { normalizeSurveyAnswersForSave } from "@/lib/survey-relevance";

const uid = () => Math.random().toString(36).slice(2, 10);
const delay = (ms = 150) => new Promise((r) => setTimeout(r, ms));

interface Store {
  profiles: Map<string, EmployeePayrollProfile>;
  surveys: Map<string, TaxAllowanceSurvey>; // by id
  latestByEmployee: Map<string, string>; // employeeId -> surveyId
}
const store: Store = { profiles: new Map(), surveys: new Map(), latestByEmployee: new Map() };

export function emptyAnswers(): TaxAllowanceSurveyAnswers {
  return {
    monthlyAllowancePreference: "AnnualReturnOnly",
    maritalStatus: "Single",
    marriageDate: null,
    firstMarriageStatus: "No",
    familyAllowanceEligibleChildrenCount: 0,
    dependentStudentCount: 0,
    hasFetusAfterDay91: false,
    fetusEligibilityMonth: null,
    hasDisabledDependent: false,
    hasSharedCustodyChild: false,
    familyAllowanceClaimMode: "NotRequested",
    otherEligiblePersonClaimsPart: "No",
    isBiologicalOrAdoptiveMother: false,
    motherAllowanceQualifyingChildrenCount: "None",
    hasCurrentOwnChildOrFetusEligibleForFamilyAllowance: "No",
    personalAllowanceEligibility: "No",
    personalAllowanceStartMonth: null,
    hasOtherEmployerOrRegularPayer: "No",
    under25AllowanceOptOut: "No",
    foreignTaxResidencyOrSimilarForeignBenefit: "None",
  };
}

function nowUtc() {
  return new Date().toISOString();
}

function ensureProfile(employeeId: string): EmployeePayrollProfile {
  const existing = store.profiles.get(employeeId);
  if (existing) return existing;
  const p: EmployeePayrollProfile = {
    id: `pp-${uid()}`,
    employeeId,
    employeeNumber: "",
    maskedTaxIdentificationNumber: "",
    taxIdentificationNumber: null,
    employmentStartDate: new Date().toISOString().slice(0, 10),
    payrollExternalId: null,
    status: "Draft",
    version: 1,
    createdAtUtc: nowUtc(),
    updatedAtUtc: nowUtc(),
  };
  store.profiles.set(employeeId, p);
  return p;
}

function deriveRequirements(
  employeeId: string,
  surveyId: string,
  answers: TaxAllowanceSurveyAnswers,
): TaxDeclarationRequirement[] {
  const today = new Date().toISOString().slice(0, 10);
  const reqs: TaxDeclarationRequirement[] = [];
  const push = (type: TaxDeclarationRequirement["type"], requiredDecision: boolean) => {
    reqs.push({
      id: `dr-${uid()}`,
      employeeId,
      surveyId,
      type,
      requiredDecision,
      status: requiredDecision ? "Required" : "NotRequired",
      effectiveFrom: today,
      effectiveTo: null,
      notes: null,
      generatedByRuleVersion: "mock-1.0.0",
      manualOverride: false,
      manualOverrideReason: null,
      version: 1,
      createdAtUtc: nowUtc(),
      updatedAtUtc: nowUtc(),
    });
  };
  push("Under25OptOut", answers.under25AllowanceOptOut === "Yes");
  push(
    "FamilyAllowance",
    answers.familyAllowanceEligibleChildrenCount > 0 &&
      answers.familyAllowanceClaimMode !== "NotRequested",
  );
  push("FirstMarriage", answers.firstMarriageStatus === "Yes");
  push("PersonalAllowance", answers.personalAllowanceEligibility === "Yes");
  push(
    "MultiChildMotherAllowance",
    answers.isBiologicalOrAdoptiveMother &&
      ["Three", "FourPlus"].includes(answers.motherAllowanceQualifyingChildrenCount),
  );
  push("Anyacska", answers.isBiologicalOrAdoptiveMother);
  return reqs;
}

function computeSummary(employeeId: string, displayName: string): PayrollOnboardingSummary {
  const profile = store.profiles.get(employeeId) ?? null;
  const surveyId = store.latestByEmployee.get(employeeId);
  const survey = surveyId ? (store.surveys.get(surveyId) ?? null) : null;
  const reqs = survey?.declarationRequirements ?? [];
  const required = reqs.filter((r) => r.requiredDecision).length;
  const outstanding = reqs.filter(
    (r) => r.requiredDecision && !["Applied", "Verified"].includes(r.status),
  ).length;
  const isComplete =
    profile?.status === "Complete" && survey?.status === "Completed" && outstanding === 0;
  return {
    employeeId,
    employeeDisplayName: displayName,
    payrollProfile: profile,
    latestSurvey: survey,
    requiredDeclarationCount: required,
    outstandingDeclarationCount: outstanding,
    isComplete,
  };
}

function throwConflict(): never {
  const err = new Error("Elavult verzió — töltsd újra az oldalt.") as Error & {
    code?: string;
    status?: number;
  };
  err.code = "CONCURRENCY_CONFLICT";
  err.status = 409;
  throw err;
}

function assertVersion(current: number, expected: number | null | undefined) {
  if (expected === null || expected === undefined) return;
  if (current !== expected) throwConflict();
}

export function makePayrollService(getEmployeeName: (id: string) => string): PayrollService {
  const findSurveyByEmployeeYear = (
    employeeId: string,
    taxYear: number,
  ): TaxAllowanceSurvey | null => {
    for (const s of store.surveys.values()) {
      if (s.employeeId === employeeId && s.taxYear === taxYear) return s;
    }
    return null;
  };

  const withRequirements = (survey: TaxAllowanceSurvey): TaxAllowanceSurvey => ({
    ...survey,
    declarationRequirements: deriveRequirements(survey.employeeId, survey.id, survey.answers).map(
      (newReq) => {
        const prev = survey.declarationRequirements.find((p) => p.type === newReq.type);
        return prev ? { ...prev, requiredDecision: newReq.requiredDecision } : newReq;
      },
    ),
  });

  return {
    async getSummary(employeeId) {
      await delay();
      return computeSummary(employeeId, getEmployeeName(employeeId));
    },
    async getProfile(employeeId) {
      await delay();
      return store.profiles.get(employeeId) ?? null;
    },
    async updateProfile(employeeId, input) {
      await delay();
      const p = ensureProfile(employeeId);
      assertVersion(p.version, input.expectedVersion);
      const updated: EmployeePayrollProfile = {
        ...p,
        employeeNumber: input.employeeNumber,
        taxIdentificationNumber: input.taxIdentificationNumber,
        maskedTaxIdentificationNumber: maskTaxId(input.taxIdentificationNumber),
        employmentStartDate: input.employmentStartDate,
        payrollExternalId: input.payrollExternalId,
        status: input.status,
        version: p.version + 1,
        updatedAtUtc: nowUtc(),
      };
      store.profiles.set(employeeId, updated);
      return updated;
    },
    async completeOnboarding(employeeId, expectedProfileVersion) {
      await delay();
      const p = store.profiles.get(employeeId);
      if (!p) throw new Error("Nincs bérszámfejtési profil.");
      assertVersion(p.version, expectedProfileVersion);
      const updated: EmployeePayrollProfile = {
        ...p,
        status: "Complete",
        version: p.version + 1,
        updatedAtUtc: nowUtc(),
      };
      store.profiles.set(employeeId, updated);
      return computeSummary(employeeId, getEmployeeName(employeeId));
    },
    async exportOnboarding(employeeId, format) {
      await delay();
      const summary = computeSummary(employeeId, getEmployeeName(employeeId));
      if (format === "csv") {
        const p = summary.payrollProfile;
        const line = [
          "employeeId,employeeNumber,employmentStartDate,payrollExternalId,status,isComplete",
          `${employeeId},${p?.employeeNumber ?? ""},${p?.employmentStartDate ?? ""},${p?.payrollExternalId ?? ""},${p?.status ?? ""},${summary.isComplete}`,
        ].join("\n");
        return new Blob([line], { type: "text/csv" });
      }
      return new Blob([JSON.stringify(summary, null, 2)], { type: "application/json" });
    },
    async getAdminSurvey(employeeId, taxYear) {
      await delay();
      return findSurveyByEmployeeYear(employeeId, taxYear);
    },
    async listDeclarationRequirements(employeeId) {
      await delay();
      const sid = store.latestByEmployee.get(employeeId);
      if (!sid) return [];
      return store.surveys.get(sid)?.declarationRequirements ?? [];
    },
    async adminUpdateSurvey(employeeId, taxYear, input) {
      await delay();
      const answers = normalizeSurveyAnswersForSave(input.answers);
      let survey = findSurveyByEmployeeYear(employeeId, taxYear);
      if (!survey) {
        survey = {
          id: `srv-${uid()}`,
          employeeId,
          taxYear,
          formVersion: "2026.1",
          ruleSetVersion: "2026.1",
          effectiveFrom: input.effectiveFrom,
          effectiveTo: null,
          sourceMetadata: "AdminEdit",
          status: "Draft",
          answers,
          declaredAtUtc: null,
          declaredByUserId: null,
          reviewedAtUtc: null,
          reviewedByUserId: null,
          hrPayrollNote: input.hrPayrollNote,
          declarationRequirements: [],
          version: 1,
          createdAtUtc: nowUtc(),
          updatedAtUtc: nowUtc(),
        };
      } else {
        assertVersion(survey.version, input.expectedVersion);
        survey = {
          ...survey,
          effectiveFrom: input.effectiveFrom,
          answers,
          hrPayrollNote: input.hrPayrollNote,
          version: survey.version + 1,
          updatedAtUtc: nowUtc(),
        };
      }
      const withReqs = withRequirements(survey);
      store.surveys.set(withReqs.id, withReqs);
      store.latestByEmployee.set(employeeId, withReqs.id);
      return withReqs;
    },
    async adminSubmitSurvey(id, expectedVersion) {
      return transitionSurvey(id, expectedVersion, "Submitted", {
        declaredAtUtc: nowUtc(),
        declaredByUserId: "admin-mock",
      });
    },
    async adminReopenSurvey(id, expectedVersion) {
      return transitionSurvey(id, expectedVersion, "Draft");
    },
    async adminReviewSurvey(id, input) {
      return transitionSurvey(id, input.expectedVersion, "Reviewed", {
        hrPayrollNote: input.hrPayrollNote,
        reviewedAtUtc: nowUtc(),
        reviewedByUserId: "admin-mock",
      });
    },
    async adminCompleteSurvey(id, expectedVersion) {
      return transitionSurvey(id, expectedVersion, "Completed");
    },
    async updateDeclarationStatus(id, input) {
      await delay();
      const { survey, req } = findDeclaration(id);
      assertVersion(req.version, input.expectedVersion);
      const updated: TaxDeclarationRequirement = {
        ...req,
        status: input.status,
        effectiveTo: input.effectiveTo,
        notes: input.notes,
        version: req.version + 1,
        updatedAtUtc: nowUtc(),
      };
      survey.declarationRequirements = survey.declarationRequirements.map((r) =>
        r.id === id ? updated : r,
      );
      store.surveys.set(survey.id, survey);
      return updated;
    },
    async overrideDeclaration(id, input) {
      await delay();
      const { survey, req } = findDeclaration(id);
      assertVersion(req.version, input.expectedVersion);
      const updated: TaxDeclarationRequirement = {
        ...req,
        requiredDecision: input.requiredDecision,
        status: input.status,
        effectiveTo: input.effectiveTo,
        manualOverride: true,
        manualOverrideReason: input.reason,
        version: req.version + 1,
        updatedAtUtc: nowUtc(),
      };
      survey.declarationRequirements = survey.declarationRequirements.map((r) =>
        r.id === id ? updated : r,
      );
      store.surveys.set(survey.id, survey);
      return updated;
    },
    async getMyOnboarding() {
      await delay();
      const emp = currentEmployee();
      return computeSummary(emp.id, emp.displayName);
    },
    async getMySurvey(taxYear) {
      await delay();
      const emp = currentEmployee();
      return findSurveyByEmployeeYear(emp.id, taxYear);
    },
    async createMySurvey(input) {
      await delay();
      const emp = currentEmployee();
      const survey: TaxAllowanceSurvey = {
        id: `srv-${uid()}`,
        employeeId: emp.id,
        taxYear: input.taxYear,
        formVersion: "2026.1",
        ruleSetVersion: "2026.1",
        effectiveFrom: input.effectiveFrom,
        effectiveTo: null,
        sourceMetadata: "SelfService",
        status: "Draft",
        answers: normalizeSurveyAnswersForSave(input.answers),
        declaredAtUtc: null,
        declaredByUserId: null,
        reviewedAtUtc: null,
        reviewedByUserId: null,
        hrPayrollNote: null,
        declarationRequirements: [],
        version: 1,
        createdAtUtc: nowUtc(),
        updatedAtUtc: nowUtc(),
      };
      const withReqs = withRequirements(survey);
      store.surveys.set(withReqs.id, withReqs);
      store.latestByEmployee.set(emp.id, withReqs.id);
      return withReqs;
    },
    async updateMySurvey(id, input) {
      await delay();
      const survey = store.surveys.get(id);
      if (!survey) throw new Error("Nem található kérdőív.");
      assertVersion(survey.version, input.expectedVersion);
      if (survey.status !== "Draft" && survey.status !== "NeedsClarification") {
        throw new Error("Ebben az állapotban a kérdőív nem szerkeszthető.");
      }
      const updated: TaxAllowanceSurvey = {
        ...survey,
        effectiveFrom: input.effectiveFrom,
        answers: normalizeSurveyAnswersForSave(input.answers),
        version: survey.version + 1,
        updatedAtUtc: nowUtc(),
      };
      const withReqs = withRequirements(updated);
      store.surveys.set(withReqs.id, withReqs);
      return withReqs;
    },
    async submitMySurvey(id, expectedVersion) {
      return transitionSurvey(id, expectedVersion, "Submitted", {
        declaredAtUtc: nowUtc(),
        declaredByUserId: currentEmployee().linkedUserId ?? "self-mock",
      });
    },
  };

  function transitionSurvey(
    id: string,
    expectedVersion: number,
    next: TaxAllowanceSurvey["status"],
    patch: Partial<TaxAllowanceSurvey> = {},
  ): Promise<TaxAllowanceSurvey> {
    return (async () => {
      await delay();
      const survey = store.surveys.get(id);
      if (!survey) throw new Error("Nem található kérdőív.");
      assertVersion(survey.version, expectedVersion);
      const updated: TaxAllowanceSurvey = {
        ...survey,
        ...patch,
        status: next,
        version: survey.version + 1,
        updatedAtUtc: nowUtc(),
      };
      store.surveys.set(id, updated);
      return updated;
    })();
  }

  function findDeclaration(id: string): {
    survey: TaxAllowanceSurvey;
    req: TaxDeclarationRequirement;
  } {
    for (const s of store.surveys.values()) {
      const req = s.declarationRequirements.find((r) => r.id === id);
      if (req) return { survey: s, req };
    }
    throw new Error("Nem található nyilatkozat.");
  }
}

// Külső session kontextus — a mock/index.ts wire-oláskor bevezeti.
let _currentUser: () => User | null = () => null;
let _findEmployee: (id: string) => { id: string; displayName: string } | null = () => null;

export function bindPayrollMockContext(opts: {
  currentUser: () => User | null;
  findEmployee: (id: string) => { id: string; displayName: string } | null;
}) {
  _currentUser = opts.currentUser;
  _findEmployee = opts.findEmployee;
}

function currentEmployee(): { id: string; displayName: string; linkedUserId?: string } {
  const u = _currentUser();
  if (!u?.linkedEmployee) throw new Error("A felhasználódhoz nem tartozik dolgozói profil.");
  return {
    id: u.linkedEmployee.id,
    displayName: u.linkedEmployee.displayName,
    linkedUserId: u.id,
  };
}

export function getEmployeeDisplayName(id: string): string {
  return _findEmployee(id)?.displayName ?? id;
}
