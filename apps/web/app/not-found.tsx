import { ErrorPage } from "@/components/error-page";

export default function NotFound() {
  return (
    <ErrorPage
      code="404"
      eyebrow="PAGE NOT FOUND"
      title="This page does not exist."
      description="The address may be incorrect, or the page may have moved to a new location."
    />
  );
}