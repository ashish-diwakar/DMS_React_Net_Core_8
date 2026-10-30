export default function AdminPageHeading({ title, description }: { title: string; description: string }) {
  return <header className="admin-page-heading"><h1>{title}</h1><p>{description}</p></header>;
}
