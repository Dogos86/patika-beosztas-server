import { describe, it, expect } from "vitest";
import { collectAllPages, mapPagedResponse } from "./pagination";

describe("mapPagedResponse", () => {
  it("totalCount → total, tételeket leképezi", () => {
    const paged = mapPagedResponse(
      { items: [{ x: 1 }, { x: 2 }], totalCount: 42, page: 2, pageSize: 20 },
      (b) => b.x * 10,
    );
    expect(paged).toEqual({ items: [10, 20], total: 42, page: 2, pageSize: 20 });
  });
});

describe("collectAllPages", () => {
  it("több oldalon lapoz végig, nem feltételezi a teli első oldalt", async () => {
    const pages = [
      { items: [1, 2], total: 5, page: 1, pageSize: 2 },
      { items: [3, 4], total: 5, page: 2, pageSize: 2 },
      { items: [5], total: 5, page: 3, pageSize: 2 },
    ];
    const calls: number[] = [];
    const all = await collectAllPages<number>(
      async (page) => {
        calls.push(page);
        return pages[page - 1];
      },
      { pageSize: 2 },
    );
    expect(all).toEqual([1, 2, 3, 4, 5]);
    expect(calls).toEqual([1, 2, 3]);
  });

  it("felső elemszám-védelem megállítja a lapozást", async () => {
    let calls = 0;
    const all = await collectAllPages<number>(
      async (page, pageSize) => {
        calls++;
        return {
          items: Array.from({ length: pageSize }, (_, i) => (page - 1) * pageSize + i),
          total: 10_000,
          page,
          pageSize,
        };
      },
      { pageSize: 10, maxItems: 25 },
    );
    expect(all).toHaveLength(25);
    expect(calls).toBeLessThanOrEqual(3);
  });

  it("megszakítás esetén nem kér új oldalt", async () => {
    const controller = new AbortController();
    let calls = 0;
    const all = await collectAllPages<number>(
      async (page, pageSize) => {
        calls++;
        controller.abort();
        return { items: [page], total: 100, page, pageSize };
      },
      { pageSize: 1, signal: controller.signal },
    );
    expect(calls).toBe(1);
    expect(all).toEqual([1]);
  });
});
