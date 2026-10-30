export type SectionType = 'hero' | 'text' | 'image_text';
export type SectionItem = {
  id: string; pageId: string; type: SectionType; sortOrder: number; isEnabled: boolean;
  heading: string; body: string | null; mediaId: string | null; imageAlt: string | null;
  buttonLabel: string | null; buttonUrl: string | null; mediaName: string | null; mediaUrl: string | null;
};
export type SectionDraft = {
  type: SectionType; heading: string; body: string; isEnabled: boolean;
  mediaId: string | null; mediaName: string | null; mediaUrl: string | null;
  imageAlt: string; buttonLabel: string; buttonUrl: string;
};
export type SectionList = {
  page: { id: string; title: string; status: 'draft' | 'published' };
  sections: SectionItem[];
};
export const emptyDraft = (type: SectionType = 'hero'): SectionDraft => ({
  type, heading: '', body: '', isEnabled: true, mediaId: null, mediaName: null, mediaUrl: null,
  imageAlt: '', buttonLabel: '', buttonUrl: '',
});
export const sectionTypeName = (type: SectionType) =>
  ({ hero: 'Hero', text: 'Text Content', image_text: 'Image with Text' })[type];
