import { ErrorPage } from "@/components/error-page";

export default function NotAuthorisedPage() {
  return (
    <ErrorPage
      code="403"
      eyebrow="ACCESS DENIED"
      title="You are not authorised to view this page."
      description="Your account does not have the required role. Return to your dashboard or contact an administrator."
    />
  );
}