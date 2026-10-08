import { MarketSimulator } from "@/components/market-simulator";
import { ProtectedPage } from "@/components/protected-page";

// Protects the Version 1 market operations workspace with the administrator role.
export default function AdminSimulatorPage() {
  return (
    <ProtectedPage roles={["admin"]}>
      <main className="appPage">
        <div className="pageContainer">
          <MarketSimulator />
        </div>
      </main>
    </ProtectedPage>
  );
}