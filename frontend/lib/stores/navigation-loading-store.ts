import { create } from "zustand";

interface NavigationLoadingStore {
  isLoading: boolean;
  setLoading: (loading: boolean) => void;
}

export const useNavigationLoadingStore = create<NavigationLoadingStore>((set) => ({
  isLoading: false,
  setLoading: (loading: boolean) => set({ isLoading: loading }),
}));

