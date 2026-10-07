import { MarketSnapshot } from "@/components/market-snapshot";
import { ProtectedPage } from "@/components/protected-page";

export default function MarketsPage() {
  return (
    <ProtectedPage>
      <main className="appPage">
        <div className="pageContainer">
          <MarketSnapshot />
        </div>
      </main>
    </ProtectedPage>
  );
}