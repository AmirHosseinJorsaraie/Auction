import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import Home from "./page";

describe("home page", () => {
  it("renders the Persian foundation heading", () => {
    render(<Home />);
    expect(screen.getByRole("heading", { name: "هنر، با روایت و اصالت" })).toBeTruthy();
  });
});
