import { ProtectedPage } from "@/components/protected-page";
import { UserManagement } from "@/components/user-management";

export default function AdminUsersPage() {
  return (
    <ProtectedPage roles={["admin"]}>
      <main className="appPage">
        <div className="pageContainer">
          <UserManagement />
        </div>
      </main>
    </ProtectedPage>
  );
}