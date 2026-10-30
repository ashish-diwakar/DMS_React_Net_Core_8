export type PageItem = {
  id: string; title: string; slug: string; status: 'draft' | 'published';
  createdAt: string | null; updatedAt: string | null;
};
export type PageList = { items: PageItem[]; page: number; totalPages: number; total: number };
export type PageDraft = { title: string; slug: string; status: 'draft' | 'published' };
export function suggestSlug(value: string) {
  return value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
    .replace(/[_\s]+/g, '-').replace(/[^a-z0-9-]/g, '').replace(/-+/g, '-').replace(/^-|-$/g, '');
}
