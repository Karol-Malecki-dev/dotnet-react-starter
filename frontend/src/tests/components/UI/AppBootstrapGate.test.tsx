import { render, screen } from '@testing-library/react';
import { AppBootstrapGate } from '../../../components/UI/AppBootstrapGate';
import { useAuth } from '../../../hooks/useAuth';
import { useFeatureAvailability } from '../../../hooks/useFeatureAvailability';

import { vi } from 'vitest';

vi.mock('../../../hooks/useAuth');
vi.mock('../../../hooks/useFeatureAvailability');

const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedUseFeatureAvailability = useFeatureAvailability as jest.MockedFunction<typeof useFeatureAvailability>;

describe('AppBootstrapGate', () => {
  beforeEach(() => {
    jest.resetAllMocks();
  });

  it('shows a loading shell while auth or runtime config are still loading', () => {
    mockedUseAuth.mockReturnValue({ loading: true } as any);
    mockedUseFeatureAvailability.mockReturnValue({ loading: true } as any);

    render(
      <AppBootstrapGate>
        <div>Ready</div>
      </AppBootstrapGate>,
    );

    expect(screen.getByText(/loading application shell/i)).toBeInTheDocument();
    expect(screen.queryByText(/ready/i)).not.toBeInTheDocument();
  });
});