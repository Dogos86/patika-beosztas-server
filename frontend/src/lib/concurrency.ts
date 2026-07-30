/** Limitált párhuzamosságú map — nem indít korlátlan kérést egyszerre. */
export async function mapWithConcurrency<T, R>(
  items: readonly T[],
  limit: number,
  worker: (item: T, index: number) => Promise<R>,
  options?: { signal?: AbortSignal },
): Promise<R[]> {
  const results = new Array<R>(items.length);
  const size = Math.max(1, Math.min(limit, items.length || 1));
  let cursor = 0;
  async function run(): Promise<void> {
    for (;;) {
      if (options?.signal?.aborted) return;
      const index = cursor++;
      if (index >= items.length) return;
      results[index] = await worker(items[index], index);
    }
  }
  await Promise.all(Array.from({ length: size }, run));
  return results;
}
