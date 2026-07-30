import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { services } from "@/services";
import type {
  FamilyAllowanceClaimMode,
  MaritalStatus,
  MonthlyAllowancePreference,
  MotherAllowanceQualifyingChildrenCount,
  SurveyAnswer,
  TaxAllowanceSurvey,
  TaxAllowanceSurveyAnswers,
  Under25AllowanceOptOut,
  ForeignTaxResidencyOrSimilarForeignBenefit,
} from "@/services/types";
import {
  familyAllowanceClaimModeLabel,
  foreignTaxLabel,
  maritalStatusLabel,
  monthlyAllowancePreferenceLabel,
  motherChildCountLabel,
  surveyAnswerLabel,
  surveyStatusLabel,
  under25OptOutLabel,
} from "@/lib/payroll-labels";
import { emptyAnswers } from "@/services/mock/payroll";
import { ConditionalField } from "@/components/common/ConditionalField";
import { isFieldRelevant, normalizeSurveyAnswersForSave } from "@/lib/survey-relevance";
import { Separator } from "@/components/ui/separator";

type Mode = "self" | "admin";

interface Props {
  mode: Mode;
  employeeId: string;
  taxYear: number;
  survey: TaxAllowanceSurvey | null;
}

const YES_NO_UNKNOWN: SurveyAnswer[] = ["Yes", "No", "Unknown"];
const MONTHLY_PREFS: MonthlyAllowancePreference[] = [
  "ApplyMonthly",
  "AnnualReturnOnly",
  "NeedsConsultation",
];
const MARITAL_STATUSES: MaritalStatus[] = [
  "Single",
  "Married",
  "Partnership",
  "Divorced",
  "Widowed",
  "Other",
];
const CLAIM_MODES: FamilyAllowanceClaimMode[] = ["NotRequested", "Alone", "Shared", "Undecided"];
const CHILD_COUNTS: MotherAllowanceQualifyingChildrenCount[] = [
  "None",
  "One",
  "Two",
  "Three",
  "FourPlus",
  "Unknown",
];
const UNDER25: Under25AllowanceOptOut[] = ["No", "Yes", "NeedsConsultation"];
const FOREIGN: ForeignTaxResidencyOrSimilarForeignBenefit[] = ["None", "PresentNeedsConsultation"];

export function TaxAllowanceSurveyForm({ mode, employeeId, taxYear, survey }: Props) {
  const qc = useQueryClient();
  const [effectiveFrom, setEffectiveFrom] = useState(survey?.effectiveFrom ?? `${taxYear}-01-01`);
  const [answers, setAnswers] = useState<TaxAllowanceSurveyAnswers>(
    survey?.answers ?? emptyAnswers(),
  );
  const [hrNote, setHrNote] = useState(survey?.hrPayrollNote ?? "");

  useEffect(() => {
    if (survey) {
      setEffectiveFrom(survey.effectiveFrom);
      setAnswers(survey.answers);
      setHrNote(survey.hrPayrollNote ?? "");
    }
  }, [survey]);

  const readOnly =
    mode === "self" && survey !== null && !["Draft", "NeedsClarification"].includes(survey.status);

  const setA = <K extends keyof TaxAllowanceSurveyAnswers>(k: K, v: TaxAllowanceSurveyAnswers[K]) =>
    setAnswers({ ...answers, [k]: v });

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["payroll-summary", employeeId] });
    qc.invalidateQueries({ queryKey: ["payroll-survey", employeeId, taxYear] });
    qc.invalidateQueries({ queryKey: ["my-onboarding"] });
    qc.invalidateQueries({ queryKey: ["my-survey", taxYear] });
  };

  const saveMut = useMutation({
    mutationFn: async () => {
      const normalized = normalizeSurveyAnswersForSave(answers);
      if (mode === "admin") {
        return services.payroll.adminUpdateSurvey(employeeId, taxYear, {
          effectiveFrom,
          answers: normalized,
          hrPayrollNote: hrNote.trim() ? hrNote.trim() : null,
          expectedVersion: survey?.version ?? null,
        });
      }
      if (!survey) {
        return services.payroll.createMySurvey({
          taxYear,
          effectiveFrom,
          answers: normalized,
        });
      }
      return services.payroll.updateMySurvey(survey.id, {
        effectiveFrom,
        answers: normalized,
        expectedVersion: survey.version,
      });
    },
    onSuccess: () => {
      toast.success("Kérdőív mentve.");
      invalidate();
    },
    onError: (e) => toast.error("Mentés sikertelen.", { description: (e as Error).message }),
  });

  const submitMut = useMutation({
    mutationFn: async () => {
      if (!survey) throw new Error("Először mentsd a kérdőívet.");
      return mode === "admin"
        ? services.payroll.adminSubmitSurvey(survey.id, survey.version)
        : services.payroll.submitMySurvey(survey.id, survey.version);
    },
    onSuccess: () => {
      toast.success("Beadva.");
      invalidate();
    },
    onError: (e) => toast.error((e as Error).message),
  });

  const reviewMut = useMutation({
    mutationFn: async () => {
      if (!survey) throw new Error("Nincs mit ellenőrizni.");
      return services.payroll.adminReviewSurvey(survey.id, {
        hrPayrollNote: hrNote.trim() ? hrNote.trim() : null,
        expectedVersion: survey.version,
      });
    },
    onSuccess: () => {
      toast.success("Ellenőrizve.");
      invalidate();
    },
    onError: (e) => toast.error((e as Error).message),
  });

  const completeMut = useMutation({
    mutationFn: async () => {
      if (!survey) throw new Error("Nincs mit lezárni.");
      return services.payroll.adminCompleteSurvey(survey.id, survey.version);
    },
    onSuccess: () => {
      toast.success("Lezárva.");
      invalidate();
    },
    onError: (e) => toast.error((e as Error).message),
  });

  const reopenMut = useMutation({
    mutationFn: async () => {
      if (!survey) throw new Error("Nincs mit visszanyitni.");
      return services.payroll.adminReopenSurvey(survey.id, survey.version);
    },
    onSuccess: () => {
      toast.success("Piszkozattá visszanyitva.");
      invalidate();
    },
    onError: (e) => toast.error((e as Error).message),
  });

  const busy =
    saveMut.isPending ||
    submitMut.isPending ||
    reviewMut.isPending ||
    completeMut.isPending ||
    reopenMut.isPending;
  const disabled = readOnly || busy;

  const rel = {
    marriageDate: isFieldRelevant("marriageDate", answers),
    fetusEligibilityMonth: isFieldRelevant("fetusEligibilityMonth", answers),
    otherEligiblePersonClaimsPart: isFieldRelevant("otherEligiblePersonClaimsPart", answers),
    motherAllowanceQualifyingChildrenCount: isFieldRelevant(
      "motherAllowanceQualifyingChildrenCount",
      answers,
    ),
    personalAllowanceStartMonth: isFieldRelevant("personalAllowanceStartMonth", answers),
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between flex-wrap gap-2">
        <div>
          <CardTitle>Adókedvezmény-nyilatkozat ({taxYear})</CardTitle>
          {survey && (
            <p className="text-xs text-muted-foreground mt-1">
              Verzió {survey.version} · Szabálykészlet {survey.ruleSetVersion}
            </p>
          )}
        </div>
        {survey && <Badge variant="outline">{surveyStatusLabel(survey.status)}</Badge>}
      </CardHeader>
      <CardContent className="space-y-6">
        <Section title="Alapbeállítások">
          <div className="space-y-2">
            <Label>Érvényesség kezdete</Label>
            <Input
              type="date"
              value={effectiveFrom}
              onChange={(e) => setEffectiveFrom(e.target.value)}
              disabled={disabled}
            />
          </div>
          <SelectField
            label="Havi érvényesítés"
            value={answers.monthlyAllowancePreference}
            options={MONTHLY_PREFS}
            labelFn={monthlyAllowancePreferenceLabel}
            onChange={(v) => setA("monthlyAllowancePreference", v)}
            disabled={disabled}
          />
        </Section>

        <Section title="Családi állapot és első házasok">
          <SelectField
            label="Családi állapot"
            value={answers.maritalStatus}
            options={MARITAL_STATUSES}
            labelFn={maritalStatusLabel}
            onChange={(v) => setA("maritalStatus", v)}
            disabled={disabled}
          />
          <ConditionalField relevant={rel.marriageDate} disabled={disabled}>
            <Label>Házasságkötés dátuma</Label>
            <Input
              type="date"
              value={answers.marriageDate ?? ""}
              onChange={(e) => setA("marriageDate", e.target.value || null)}
              disabled={disabled || !rel.marriageDate}
            />
          </ConditionalField>
          <SelectField
            label="Első házasok kedvezménye"
            value={answers.firstMarriageStatus}
            options={YES_NO_UNKNOWN}
            labelFn={surveyAnswerLabel}
            onChange={(v) => setA("firstMarriageStatus", v)}
            disabled={disabled}
          />
        </Section>

        <Section title="Gyermekek, eltartottak és családi kedvezmény">
          <NumberField
            label="Családi kedvezményre jogosító gyermek"
            value={answers.familyAllowanceEligibleChildrenCount}
            onChange={(v) => setA("familyAllowanceEligibleChildrenCount", v)}
            disabled={disabled}
          />
          <NumberField
            label="Eltartott tanuló"
            value={answers.dependentStudentCount}
            onChange={(v) => setA("dependentStudentCount", v)}
            disabled={disabled}
          />
          <SwitchField
            label="Van 91. napot betöltött magzat"
            checked={answers.hasFetusAfterDay91}
            onChange={(v) => setA("hasFetusAfterDay91", v)}
            disabled={disabled}
          />
          <ConditionalField relevant={rel.fetusEligibilityMonth} disabled={disabled}>
            <Label>Magzati kedvezmény kezdete (hó)</Label>
            <Input
              type="month"
              value={answers.fetusEligibilityMonth ?? ""}
              onChange={(e) => setA("fetusEligibilityMonth", e.target.value || null)}
              disabled={disabled || !rel.fetusEligibilityMonth}
            />
          </ConditionalField>
          <SwitchField
            label="Fogyatékos eltartott"
            checked={answers.hasDisabledDependent}
            onChange={(v) => setA("hasDisabledDependent", v)}
            disabled={disabled}
          />
          <SwitchField
            label="Megosztott felügyeletű gyermek"
            checked={answers.hasSharedCustodyChild}
            onChange={(v) => setA("hasSharedCustodyChild", v)}
            disabled={disabled}
          />
          <SelectField
            label="Családi kedvezmény érvényesítés"
            value={answers.familyAllowanceClaimMode}
            options={CLAIM_MODES}
            labelFn={familyAllowanceClaimModeLabel}
            onChange={(v) => setA("familyAllowanceClaimMode", v)}
            disabled={disabled}
          />
          <ConditionalField relevant={rel.otherEligiblePersonClaimsPart} disabled={disabled}>
            <SelectField
              label="Más jogosult rész-igénylése"
              value={answers.otherEligiblePersonClaimsPart}
              options={YES_NO_UNKNOWN}
              labelFn={surveyAnswerLabel}
              onChange={(v) => setA("otherEligiblePersonClaimsPart", v)}
              disabled={disabled || !rel.otherEligiblePersonClaimsPart}
            />
          </ConditionalField>
        </Section>

        <Section title="Anyák kedvezményei">
          <SwitchField
            label="Biológiai/örökbefogadó anya"
            checked={answers.isBiologicalOrAdoptiveMother}
            onChange={(v) => setA("isBiologicalOrAdoptiveMother", v)}
            disabled={disabled}
          />
          <ConditionalField
            relevant={rel.motherAllowanceQualifyingChildrenCount}
            disabled={disabled}
          >
            <SelectField
              label="Anya-kedvezményre jogosító gyerekek"
              value={answers.motherAllowanceQualifyingChildrenCount}
              options={CHILD_COUNTS}
              labelFn={motherChildCountLabel}
              onChange={(v) => setA("motherAllowanceQualifyingChildrenCount", v)}
              disabled={disabled || !rel.motherAllowanceQualifyingChildrenCount}
            />
          </ConditionalField>
          <SelectField
            label="Van jelenleg jogosult gyermek/magzat"
            value={answers.hasCurrentOwnChildOrFetusEligibleForFamilyAllowance}
            options={YES_NO_UNKNOWN}
            labelFn={surveyAnswerLabel}
            onChange={(v) => setA("hasCurrentOwnChildOrFetusEligibleForFamilyAllowance", v)}
            disabled={disabled}
          />
        </Section>

        <Section title="Egyéb kedvezmények és körülmények">
          <SelectField
            label="Személyi kedvezményre jogosult"
            value={answers.personalAllowanceEligibility}
            options={YES_NO_UNKNOWN}
            labelFn={surveyAnswerLabel}
            onChange={(v) => setA("personalAllowanceEligibility", v)}
            disabled={disabled}
          />
          <ConditionalField relevant={rel.personalAllowanceStartMonth} disabled={disabled}>
            <Label>Személyi kedvezmény kezdete (hó)</Label>
            <Input
              type="month"
              value={answers.personalAllowanceStartMonth ?? ""}
              onChange={(e) => setA("personalAllowanceStartMonth", e.target.value || null)}
              disabled={disabled || !rel.personalAllowanceStartMonth}
            />
          </ConditionalField>
          <SelectField
            label="Másik munkáltatónál is dolgozik"
            value={answers.hasOtherEmployerOrRegularPayer}
            options={YES_NO_UNKNOWN}
            labelFn={surveyAnswerLabel}
            onChange={(v) => setA("hasOtherEmployerOrRegularPayer", v)}
            disabled={disabled}
          />
          <SelectField
            label="25 alatti — lemondó nyilatkozat"
            value={answers.under25AllowanceOptOut}
            options={UNDER25}
            labelFn={under25OptOutLabel}
            onChange={(v) => setA("under25AllowanceOptOut", v)}
            disabled={disabled}
          />
          <SelectField
            label="Külföldi adóügyi illetőség / kedvezmény"
            value={answers.foreignTaxResidencyOrSimilarForeignBenefit}
            options={FOREIGN}
            labelFn={foreignTaxLabel}
            onChange={(v) => setA("foreignTaxResidencyOrSimilarForeignBenefit", v)}
            disabled={disabled}
          />
        </Section>

        {mode === "admin" && (
          <>
            <Separator />
            <div className="space-y-2">
              <Label>HR / bérszámfejtő megjegyzés</Label>
              <Textarea
                rows={2}
                value={hrNote}
                onChange={(e) => setHrNote(e.target.value)}
                disabled={busy}
              />
            </div>
          </>
        )}

        <div className="flex flex-wrap gap-2 justify-end">
          {!readOnly && (
            <Button onClick={() => saveMut.mutate()} disabled={busy}>
              Kérdőív mentése
            </Button>
          )}
          {survey && mode === "self" && survey.status === "Draft" && (
            <Button variant="secondary" onClick={() => submitMut.mutate()} disabled={busy}>
              Beadás
            </Button>
          )}
          {survey && mode === "admin" && (
            <>
              {survey.status === "Draft" && (
                <Button variant="secondary" onClick={() => submitMut.mutate()} disabled={busy}>
                  Beadás
                </Button>
              )}
              {(survey.status === "Submitted" || survey.status === "NeedsClarification") && (
                <Button variant="secondary" onClick={() => reviewMut.mutate()} disabled={busy}>
                  Ellenőrzés jóváhagyása
                </Button>
              )}
              {survey.status === "Reviewed" && (
                <Button onClick={() => completeMut.mutate()} disabled={busy}>
                  Lezárás
                </Button>
              )}
              {survey.status !== "Draft" && survey.status !== "Completed" && (
                <Button variant="ghost" onClick={() => reopenMut.mutate()} disabled={busy}>
                  Visszanyitás piszkozatra
                </Button>
              )}
            </>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-3">
      <h3 className="text-sm font-semibold text-foreground">{title}</h3>
      <div className="grid gap-3 md:grid-cols-2">{children}</div>
    </section>
  );
}

function SelectField<T extends string>({
  label,
  value,
  options,
  labelFn,
  onChange,
  disabled,
}: {
  label: string;
  value: T;
  options: T[];
  labelFn: (v: T) => string;
  onChange: (v: T) => void;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Select value={value} onValueChange={(v) => onChange(v as T)} disabled={disabled}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map((o) => (
            <SelectItem key={o} value={o}>
              {labelFn(o)}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

function NumberField({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Input
        type="number"
        min={0}
        value={value}
        onChange={(e) => onChange(Math.max(0, Number(e.target.value) || 0))}
        disabled={disabled}
      />
    </div>
  );
}

function SwitchField({
  label,
  checked,
  onChange,
  disabled,
}: {
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <label className="flex items-center justify-between gap-3 text-sm">
      <span>{label}</span>
      <Switch checked={checked} onCheckedChange={onChange} disabled={disabled} />
    </label>
  );
}
