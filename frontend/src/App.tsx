import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { RuntimeConfigProvider } from './context/RuntimeConfigContext';
import { AppBootstrapGate } from './components/UI/AppBootstrapGate';
import { AppShell } from './components/AppShell';

export default function App() {
  return (
    <BrowserRouter>
      <RuntimeConfigProvider>
        <AuthProvider>
          <AppBootstrapGate>
            <AppShell />
          </AppBootstrapGate>
        </AuthProvider>
      </RuntimeConfigProvider>
    </BrowserRouter>
  );
}
