import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { GoogleSignInButton } from "./GoogleSignInButton";
import { tr } from "@/lib/i18n/tr";

const { googleAvailableMock, startGoogleSignInMock } = vi.hoisted(() => ({
  googleAvailableMock: vi.fn(),
  startGoogleSignInMock: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  authApi: {
    googleAvailable: googleAvailableMock,
    startGoogleSignIn: startGoogleSignInMock,
  },
}));

describe("GoogleSignInButton", () => {
  it("renders nothing while availability is unknown", () => {
    googleAvailableMock.mockReturnValue(new Promise(() => undefined));
    const { container } = render(<GoogleSignInButton />);
    expect(container).toBeEmptyDOMElement();
  });

  it("stays hidden when the server has no Google credentials", async () => {
    googleAvailableMock.mockResolvedValue(false);
    render(<GoogleSignInButton />);
    // Give the resolved promise a chance to flush before asserting absence.
    await Promise.resolve();
    expect(screen.queryByRole("button", { name: tr.auth.googleSignIn })).not.toBeInTheDocument();
  });

  it("shows the button and starts the flow with the requested return path", async () => {
    const user = userEvent.setup();
    googleAvailableMock.mockResolvedValue(true);

    render(<GoogleSignInButton returnPath="/aliskanliklar" />);
    const button = await screen.findByRole("button", { name: tr.auth.googleSignIn });
    await user.click(button);

    expect(startGoogleSignInMock).toHaveBeenCalledWith("/aliskanliklar");
  });

  it("defaults the return path to the dashboard", async () => {
    const user = userEvent.setup();
    googleAvailableMock.mockResolvedValue(true);

    render(<GoogleSignInButton />);
    await user.click(await screen.findByRole("button", { name: tr.auth.googleSignIn }));

    expect(startGoogleSignInMock).toHaveBeenCalledWith("/");
  });
});
