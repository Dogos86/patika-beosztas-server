import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TaxAllowanceSurveyForm } from "./TaxAllowanceSurveyForm";

vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

function renderForm() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <TaxAllowanceSurveyForm mode="admin" employeeId="e1" taxYear={2026} survey={null} />
    </QueryClientProvider>,
  );
}

function fieldsetFor(labelText: string): HTMLFieldSetElement {
  const labels = screen.getAllByText(labelText).filter((el) => el.tagName === "LABEL");
  if (labels.length === 0) throw new Error(`Nincs LABEL: ${labelText}`);
  const fs = labels[0].closest("fieldset");
  if (!fs) throw new Error(`Nincs fieldset a(z) ${labelText} label körül`);
  return fs as HTMLFieldSetElement;
}

describe("TaxAllowanceSurveyForm — stabil elrendezés", () => {
  beforeEach(() => vi.clearAllMocks());

  it("minden feltételes mező alapból is a DOM-ban van", () => {
    renderForm();
    expect(screen.getByText("Házasságkötés dátuma")).toBeTruthy();
    expect(screen.getByText("Magzati kedvezmény kezdete (hó)")).toBeTruthy();
    expect(screen.getByText("Más jogosult rész-igénylése")).toBeTruthy();
    expect(screen.getByText("Anya-kedvezményre jogosító gyerekek")).toBeTruthy();
    expect(screen.getByText("Személyi kedvezmény kezdete (hó)")).toBeTruthy();
  });

  it("irreleváns mező disabled/aria-disabled és megjeleníti a segédszöveget", () => {
    renderForm();
    const fs = fieldsetFor("Házasságkötés dátuma");
    expect(fs.disabled).toBe(true);
    expect(fs.getAttribute("aria-disabled")).toBe("true");
    expect(fs.dataset.relevant).toBe("false");
    expect(
      screen.getAllByText("Az előző válasz alapján ez a mező jelenleg nem releváns.").length,
    ).toBeGreaterThan(0);
  });

  it("magzat kapcsoló bekapcsolásakor ugyanaz a fieldset aktívvá válik, sorrend nem változik", () => {
    renderForm();
    const beforeFieldsets = document.querySelectorAll("fieldset").length;
    const fs = fieldsetFor("Magzati kedvezmény kezdete (hó)");
    expect(fs.dataset.relevant).toBe("false");

    const sw = screen
      .getAllByRole("switch")
      .find((el) => el.closest("label")?.textContent?.includes("Van 91. napot betöltött magzat"));
    if (!sw) throw new Error("Nem található a magzat switch.");
    fireEvent.click(sw);

    const fsAfter = fieldsetFor("Magzati kedvezmény kezdete (hó)");
    expect(fsAfter.dataset.relevant).toBe("true");
    expect(fsAfter.disabled).toBe(false);
    expect(document.querySelectorAll("fieldset").length).toBe(beforeFieldsets);
  });
});
