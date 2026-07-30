import { describe, it, expect } from "vitest";
import { mapWithConcurrency } from "./concurrency";

describe("mapWithConcurrency", () => {
  it("nem lép túl a megadott párhuzamosságon és sorrendhelyes", async () => {
    let active = 0;
    let peak = 0;
    const items = Array.from({ length: 10 }, (_, i) => i);
    const out = await mapWithConcurrency(items, 3, async (i) => {
      active++;
      peak = Math.max(peak, active);
      await new Promise((r) => setTimeout(r, 1));
      active--;
      return i * 2;
    });
    expect(peak).toBeLessThanOrEqual(3);
    expect(out).toEqual(items.map((i) => i * 2));
  });

  it("megszakítható", async () => {
    const controller = new AbortController();
    let calls = 0;
    const items = Array.from({ length: 20 }, (_, i) => i);
    const p = mapWithConcurrency(
      items,
      2,
      async (i) => {
        calls++;
        if (i === 1) controller.abort();
        return i;
      },
      { signal: controller.signal },
    );
    await p;
    expect(calls).toBeLessThan(20);
  });
});
