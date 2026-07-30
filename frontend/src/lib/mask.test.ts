import { describe, it, expect } from "vitest";
import { maskTaxId, maskTaxIdCompact } from "./mask";

describe("maskTaxId", () => {
  it("üres bemenetre üres stringet ad", () => {
    expect(maskTaxId(null)).toBe("");
    expect(maskTaxId(undefined)).toBe("");
    expect(maskTaxId("")).toBe("");
  });
  it("hosszú stringnél csak az utolsó 3 karakter látszik", () => {
    expect(maskTaxId("8123456789")).toBe("•••••••789");
  });
  it("3 karakternél rövidebb bemenetet érintetlenül hagy", () => {
    expect(maskTaxId("12")).toBe("12");
    expect(maskTaxId("123")).toBe("123");
  });
  it("kompakt forma mindig 3 pont + utolsó 3", () => {
    expect(maskTaxIdCompact("8123456789")).toBe("•••789");
  });
});
