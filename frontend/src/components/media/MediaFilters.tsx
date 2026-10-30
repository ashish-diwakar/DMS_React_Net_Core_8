import type { FormEvent } from 'react';

export type MediaFilterType = 'all' | 'images' | 'documents';

export default function MediaFilters({ query, setQuery, draftType, setDraftType, applyFilters, resetFilters }: {
  query: string;
  setQuery: (value: string) => void;
  draftType: MediaFilterType;
  setDraftType: (value: MediaFilterType) => void;
  applyFilters: (event: FormEvent<HTMLFormElement>) => void;
  resetFilters: () => void;
}) {
  return <form className="media-filter-form" onSubmit={applyFilters} aria-label="Filter media">
    <label className="media-filter-field" htmlFor="media-search">Search display names or filenames
      <input id="media-search" type="search" maxLength={255} value={query} onChange={event => setQuery(event.target.value)} />
    </label>
    <label className="media-filter-field" htmlFor="media-type">Type
      <select id="media-type" value={draftType} onChange={event => setDraftType(event.target.value as MediaFilterType)}>
        <option value="all">All</option><option value="images">Images</option><option value="documents">Documents</option>
      </select>
    </label>
    <div className="media-filter-actions">
      <button className="media-primary-button" type="submit">Apply</button>
      <button className="media-reset-button" type="button" onClick={resetFilters}>Reset</button>
    </div>
  </form>;
}
