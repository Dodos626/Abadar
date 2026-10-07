import Link from "next/link";

export function ErrorPage({
  code,
  eyebrow,
  title,
  description,
}: {
  code: string;
  eyebrow: string;
  title: string;
  description: string;
}) {
  return (
    <main className="errorPage">
      <div className="errorArt" aria-hidden="true">{code}</div>
      <div className="errorContent">
        <p className="eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        <p>{description}</p>
        <div className="heroActions">
          <Link className="button buttonPrimary" href="/">Go home</Link>
          <Link className="button buttonSecondary" href="/dashboard">Open dashboard</Link>
        </div>
      </div>
    </main>
  );
}