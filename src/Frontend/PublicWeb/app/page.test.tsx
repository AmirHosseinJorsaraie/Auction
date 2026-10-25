import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import Home from "./page";

vi.mock("@/lib/public-api", () => ({
  publicApi: vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 6, total: 0, totalPages: 0 }),
  formatMoney: (amount: number, currency: string) => `${amount} ${currency}`,
  mediaUrl: () => "/artwork-placeholder.svg",
}));

describe("home page", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders the Persian gallery heading", async () => {
    render(await Home());
    expect(screen.getByRole("heading", { name: "هنر، با روایت و اصالت" })).toBeTruthy();
  });
});
