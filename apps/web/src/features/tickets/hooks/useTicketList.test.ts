import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ticketsApi } from "../../../api/tickets";
import { page, ticketListItem } from "../../../test/fixtures";
import { useTicketList } from "./useTicketList";

vi.mock("../../../api/tickets", () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe("useTicketList", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it("searches with the text the user typed, once the typing settles", async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: "invoice" });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: "invoice", page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  it("requests tickets immediately when status changes without changing search", async () => {
    const { result } = renderHook(() => useTicketList());

    // Allow the original initial request to complete.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // Change only the Status dropdown value.
    act(() => {
      result.current.updateFilters({ status: "InProgress" });
    });

    // Flush React updates and resolved API promises.
    await act(async () => {
      await Promise.resolve();
    });

    // A non-search filter must trigger a request immediately.
    expect(getTickets).toHaveBeenCalledTimes(2);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: "InProgress",
        page: 1,
      }),
    );
  });
  
});
