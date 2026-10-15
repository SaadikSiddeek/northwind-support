import { useEffect, useState } from "react";
import { ApiError } from "../../../api/client";
import { ticketsApi } from "../../../api/tickets";
import type {
  PagedResult,
  TicketFilters,
  TicketListItem,
} from "../../../types/api";

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: "",
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: "createdAtUtc",
  sortDirection: "desc",
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/**
 * Owns the ticket list filters and loads matching tickets.
 * Only search text is debounced; all other filters apply immediately.
 */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);

  const [debouncedSearch, setDebouncedSearch] = useState(emptyFilters.search);

  const [state, setState] = useState<State>({
    isLoading: true,
  });

  // Debounce only the search text.
  useEffect(() => {
    if (filters.search === debouncedSearch) {
      return;
    }

    const handle = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search, debouncedSearch]);

  // Build the effective query from the current filters.
  const query: TicketFilters = {
    ...filters,
    search: debouncedSearch,
  };

  const {
    search,
    status,
    priority,
    categoryId,
    customerId,
    assignedAgentId,
    unassignedOnly,
    page,
    pageSize,
    sortBy,
    sortDirection,
  } = query;

  // Fetch when any effective query parameter changes.
  useEffect(() => {
    let cancelled = false;

    const fetchTickets = async () => {
      setState((current) => ({
        ...current,
        isLoading: true,
        error: undefined,
      }));

      try {
        const data = await ticketsApi.getTickets({
          search,
          status,
          priority,
          categoryId,
          customerId,
          assignedAgentId,
          unassignedOnly,
          page,
          pageSize,
          sortBy,
          sortDirection,
        });

        if (!cancelled) {
          setState({
            isLoading: false,
            data,
            error: undefined,
          });
        }
      } catch (error) {
        if (!cancelled) {
          const message =
            error instanceof ApiError
              ? error.message
              : "Unable to load tickets.";

          setState((current) => ({
            ...current,
            isLoading: false,
            error: message,
          }));
        }
      }
    };

    void fetchTickets();

    return () => {
      cancelled = true;
    };
  }, [
    search,
    status,
    priority,
    categoryId,
    customerId,
    assignedAgentId,
    unassignedOnly,
    page,
    pageSize,
    sortBy,
    sortDirection,
  ]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    setFilters((current) => ({
      ...current,
      ...patch,
      page: patch.page ?? 1,
    }));
  };

  const resetFilters = () => {
    setFilters(emptyFilters);
  };

  const reload = async () => {
    setState((current) => ({
      ...current,
      isLoading: true,
      error: undefined,
    }));

    try {
      const data = await ticketsApi.getTickets(query);

      setState({
        isLoading: false,
        data,
        error: undefined,
      });
    } catch (error) {
      const message =
        error instanceof ApiError ? error.message : "Unable to load tickets.";

      setState((current) => ({
        ...current,
        isLoading: false,
        error: message,
      }));
    }
  };

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    ...state,
  };
}
 