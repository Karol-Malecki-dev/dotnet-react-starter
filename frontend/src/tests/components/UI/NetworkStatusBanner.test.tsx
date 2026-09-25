import { act, render, screen } from '@testing-library/react';
import { NetworkStatusBanner } from '../../../components/UI/NetworkStatusBanner';

describe('NetworkStatusBanner', () => {
  beforeEach(() => {
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: true,
    });
  });

  it('shows an accessible notice while the browser is offline', () => {
    render(<NetworkStatusBanner />);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    act(() => {
      Object.defineProperty(window.navigator, 'onLine', {
        configurable: true,
        value: false,
      });
      window.dispatchEvent(new Event('offline'));
    });

    expect(screen.getByRole('status')).toHaveTextContent(/you are offline/i);
  });

  it('removes the notice after the connection is restored', () => {
    Object.defineProperty(window.navigator, 'onLine', {
      configurable: true,
      value: false,
    });

    render(<NetworkStatusBanner />);
    expect(screen.getByRole('status')).toBeInTheDocument();

    act(() => {
      Object.defineProperty(window.navigator, 'onLine', {
        configurable: true,
        value: true,
      });
      window.dispatchEvent(new Event('online'));
    });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
