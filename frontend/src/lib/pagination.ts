import type { PagedResponse } from "@/services/types";

/** Backend `totalCount` → UI `total` normalizálás. */
export interface BackendPagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function mapPagedResponse<TBackend, TUi>(
  raw: BackendPagedResponse<TBackend>,
  mapItem: (item: TBackend) => TUi,
): PagedResponse<TUi> {
  return {
    items: raw.items.map(mapItem),
    total: raw.totalCount,
    page: raw.page,
    pageSize: raw.pageSize,
  };
}

export interface CollectAllOptions {
  /** Oldalméret a végiglapozáshoz. */
  pageSize?: number;
  /** Felső elemszám-védelem — efölött megállunk. */
  maxItems?: number;
  /** Megszakítás. */
  signal?: AbortSignal;
}

/**
 * Végiglapoz egy lapozott endpointon. Nem feltételezi, hogy az első oldal
 * teljes; a `totalCount` és az üres oldal is megállítja. Van felső korlát,
 * így nem lehet végtelen kérés.
 */
export async function collectAllPages<T>(
  fetchPage: (page: number, pageSize: number) => Promise<PagedResponse<T>>,
  options?: CollectAllOptions,
): Promise<T[]> {
  const pageSize = Math.max(1, options?.pageSize ?? 100);
  const maxItems = Math.max(1, options?.maxItems ?? 2000);
  const maxPages = Math.ceil(maxItems / pageSize);
  const out: T[] = [];
  for (let page = 1; page <= maxPages; page++) {
    if (options?.signal?.aborted) break;
    const res = await fetchPage(page, pageSize);
    out.push(...res.items);
    if (res.items.length === 0) break;
    if (out.length >= maxItems) break;
    if (Number.isFinite(res.total) && out.length >= res.total) break;
    if (res.items.length < pageSize) break;
  }
  return out.slice(0, maxItems);
}
