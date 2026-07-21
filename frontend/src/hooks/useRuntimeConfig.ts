import { useRuntimeConfigContext } from '../context/RuntimeConfigContext';

export function useRuntimeConfig() {
  return useRuntimeConfigContext();
}