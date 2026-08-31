import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { WorkspaceSearchResult } from '../../types';

export interface QuickSearchItem {
  label: string;
  description: string;
  to: string;
  keywords?: string[];
}

interface QuickSearchBarProps {
  items: QuickSearchItem[];
  placeholder?: string;
  label?: string;
  searchWorkspace?: (query: string, signal: AbortSignal) => Promise<{ data: { items: WorkspaceSearchResult[] } }>;
}

function matchesQuery(item: QuickSearchItem, query: string) {
  const normalizedQuery = query.trim().toLowerCase();

  if (!normalizedQuery) {
    return true;
  }

  const searchableText = [item.label, item.description, ...(item.keywords ?? [])].join(' ').toLowerCase();
  return searchableText.includes(normalizedQuery);
}

export function QuickSearchBar({ items, placeholder = 'Search pages, actions, and shortcuts', label = 'Quick search', searchWorkspace }: QuickSearchBarProps) {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [workspaceItems, setWorkspaceItems] = useState<WorkspaceSearchResult[]>([]);
  const [workspaceLoading, setWorkspaceLoading] = useState(false);

  useEffect(() => {
    const handleKeyboardShortcut = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        inputRef.current?.focus();
        setIsOpen(true);
      }
    };

    window.addEventListener('keydown', handleKeyboardShortcut);
    return () => window.removeEventListener('keydown', handleKeyboardShortcut);
  }, []);

  useEffect(() => {
    if (!searchWorkspace || query.trim().length < 2) {
      setWorkspaceItems([]);
      setWorkspaceLoading(false);
      return undefined;
    }

    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      setWorkspaceLoading(true);
      try {
        const response = await searchWorkspace(query, controller.signal);
        if (!controller.signal.aborted) setWorkspaceItems(response.data?.items ?? []);
      } catch {
        if (!controller.signal.aborted) setWorkspaceItems([]);
      } finally {
        if (!controller.signal.aborted) setWorkspaceLoading(false);
      }
    }, 250);

    return () => {
      controller.abort();
      window.clearTimeout(timer);
    };
  }, [query, searchWorkspace]);

  const filteredItems = useMemo(() => {
    return items.filter((item) => matchesQuery(item, query)).slice(0, 6);
  }, [items, query]);

  const handleSelect = (to: string) => {
    setQuery('');
    setIsOpen(false);
    navigate(to);
  };

  return (
    <div className="quick-search">
      <label className="quick-search__label">
        <span>{label}</span>
        <div className="quick-search__field">
          <input
            ref={inputRef}
            type="search"
            value={query}
            placeholder={placeholder}
            aria-label={label}
            onFocus={() => setIsOpen(true)}
            onChange={(event) => {
              setQuery(event.target.value);
              setIsOpen(true);
            }}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                setIsOpen(false);
                inputRef.current?.blur();
              }

              if (event.key === 'Enter' && filteredItems.length > 0) {
                event.preventDefault();
                handleSelect(filteredItems[0].to);
              }
            }}
          />
          <span className="quick-search__hint">Ctrl K</span>
        </div>
      </label>

      {isOpen ? (
        <div className="quick-search__panel" role="listbox" aria-label={`${label} suggestions`}>
          {filteredItems.length > 0 ? (
            filteredItems.map((item) => (
              <button
                key={item.to}
                type="button"
                className="quick-search__item"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => handleSelect(item.to)}
              >
                <strong>{item.label}</strong>
                <span>{item.description}</span>
              </button>
            ))
          ) : (
            <>
              {workspaceLoading ? <p className="quick-search__empty">Searching workspace...</p> : null}
              {!workspaceLoading && workspaceItems.length === 0 ? <p className="quick-search__empty">No matches for &quot;{query}&quot;.</p> : null}
            </>
          )}
          {workspaceItems.length > 0 ? (
            <div className="quick-search__group">
              <span className="quick-search__group-title">Workspace</span>
              {workspaceItems.map((item) => (
                <button key={item.resourceId} type="button" className="quick-search__item" onMouseDown={(event) => event.preventDefault()} onClick={() => handleSelect(`/projects?projectId=${item.projectId}`)}>
                  <strong>{item.title}</strong>
                  <span>{item.context || 'Project task'}</span>
                </button>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}