import { AccountForm } from "@/components/account-form";
import { ProtectedPage } from "@/components/protected-page";

export default function AccountPage() {
  return (
    <ProtectedPage>
      <main className="appPage">
        <div className="pageContainer">
          <AccountForm />
        </div>
      </main>
    </ProtectedPage>
  );
}