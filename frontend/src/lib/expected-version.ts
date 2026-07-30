/** UI Employee-hez tartozó version → backend expectedVersion request mező. */
export function withExpectedVersion<T extends object>(
  payload: T,
  version: number | undefined,
): T & { expectedVersion?: number } {
  if (version === undefined) return payload;
  return { ...payload, expectedVersion: version };
}
