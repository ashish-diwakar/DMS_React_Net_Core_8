import type { FormEvent, RefObject } from 'react';

export default function MediaUploadForm({ fileInput, busy, upload }: {
  fileInput: RefObject<HTMLInputElement | null>;
  busy: boolean;
  upload: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return <section className="media-toolbar" aria-labelledby="media-upload-heading">
    <h2 id="media-upload-heading">Upload media</h2>
    <p id="media-upload-help">Choose up to 10 files. JPEG, PNG or WebP up to 5 MiB each. PDF up to 10 MiB each. Uploaded files are public. Server request limits may restrict the total upload size.</p>
    <form className="media-upload-form" onSubmit={upload}>
      <input ref={fileInput} type="file" accept=".jpg,.jpeg,.png,.webp,.pdf" multiple required disabled={busy} aria-label="Choose media files" aria-describedby="media-upload-help" />
      <button className="media-primary-button" type="submit" disabled={busy}>{busy ? 'Uploading…' : 'Upload'}</button>
    </form>
  </section>;
}
