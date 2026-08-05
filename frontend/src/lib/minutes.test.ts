import { describe, expect, it } from "vitest";
import { hoursAndMinutesToMinutes, splitMinutes } from "./minutes";

describe("óra/perc átalakítás", () => {
  it.each([
    [12, 0, 720],
    [8, 0, 480],
    [4, 0, 240],
  ])("%i óra %i perc → %i perc", (hours, minutes, expected) => {
    expect(hoursAndMinutesToMinutes(hours, minutes)).toBe(expected);
    expect(splitMinutes(expected)).toEqual({ hours, minutes });
  });
});
