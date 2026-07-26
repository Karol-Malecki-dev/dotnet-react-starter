// jest-dom adds custom jest matchers for asserting on DOM nodes.
// allows you to do things like:
// expect(element).toHaveTextContent(/react/i)
// learn more: https://github.com/testing-library/jest-dom
import '@testing-library/jest-dom';
import { vi } from 'vitest';

(globalThis as unknown as { jest: typeof vi }).jest = vi;

const originalWarn = console.warn;

beforeAll(() => {
		vi.spyOn(console, 'warn').mockImplementation((message?: unknown, ...optionalParams: unknown[]) => {
		if (typeof message === 'string' && message.includes('React Router Future Flag Warning')) {
			return;
		}

		originalWarn(message, ...optionalParams);
	});
});

afterAll(() => {
	(console.warn as unknown as ReturnType<typeof vi.spyOn>).mockRestore();
});
