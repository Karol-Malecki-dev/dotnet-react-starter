import type { PendingTwoFactorChallenge } from '../types';

const STORAGE_KEY = 'drs.auth.pendingTwoFactor';

export function savePendingTwoFactor(challenge: PendingTwoFactorChallenge): void {
  window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(challenge));
}

export function loadPendingTwoFactor(): PendingTwoFactorChallenge | null {
  const rawValue = window.sessionStorage.getItem(STORAGE_KEY);
  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as PendingTwoFactorChallenge;
  } catch {
    window.sessionStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

export function clearPendingTwoFactor(): void {
  window.sessionStorage.removeItem(STORAGE_KEY);
}