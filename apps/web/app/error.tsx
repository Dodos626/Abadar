"use client";

import { useEffect } from "react";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="errorPage">
      <div className="errorArt" aria-hidden="true">500</div>
      <div className="errorContent">
        <p className="eyebrow">SOMETHING WENT WRONG</p>
        <h1>We could not load this page.</h1>
        <p>Please try again. If the problem continues, return to the home page.</p>
        <button className="button buttonPrimary" type="button" onClick={reset}>Try again</button>
      </div>
    </main>
  );
}