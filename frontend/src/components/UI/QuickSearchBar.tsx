import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

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
}

function matchesQuery(item: QuickSearchItem, query: string) {
  const normalizedQuery = query.trim().toLowerCase();

  if (!normalizedQuery) {
    return true;
  }

  const searchableText = [item.label, item.description, ...(item.keywords ?? [])].join(' ').toLowerCase();
  return searchableText.includes(normalizedQuery);
}

export function QuickSearchBar({ items, placeholder = 'Search pages, actions, and shortcuts', label = 'Quick search' }: QuickSearchBarProps) {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [query, setQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);

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
            <p className="quick-search__empty">No matches for “{query}”.</p>
          )}
        </div>
      ) : null}
    </div>
  );
}