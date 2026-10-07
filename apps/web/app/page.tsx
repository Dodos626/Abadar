import Link from "next/link";

export default function Home() {
  return (
    <main>
      <section className="landingHero">
        <div className="pageContainer heroGrid">
          <div className="heroCopy">
            <p className="eyebrow">MARKET SIMULATION, BUILT FOR CLARITY</p>
            <h1>
              A better way to <span>understand markets.</span>
            </h1>
            <p className="heroLead">
              ABADAR brings market simulation, account management, and operational
              insight into one focused workspace.
            </p>
            <div className="heroActions">
              <Link className="button buttonPrimary" href="/login">
                Access your account
              </Link>
              <Link className="button buttonSecondary" href="#features">
                Explore the platform
              </Link>
            </div>
          </div>

          <div className="heroVisual" aria-label="ABADAR platform preview">
            <div className="visualGlow" />
            <div className="previewCard previewCardBack">
              <span>INFRASTRUCTURE</span>
              <strong>Ready for the next market event.</strong>
            </div>
            <div className="previewCard previewCardMain">
              <div className="previewTopline">
                <span>MARKET SNAPSHOT</span>
                <span className="liveIndicator">LIVE</span>
              </div>
              <div className="chartBars" aria-hidden="true">
                {[42, 58, 47, 68, 62, 82, 74, 91, 79, 96].map((height, index) => (
                  <i key={index} style={{ height: `${height}%` }} />
                ))}
              </div>
              <div className="previewStats">
                <div>
                  <span>BTC / USD</span>
                  <strong>$67,482.10</strong>
                </div>
                <b>+2.48%</b>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="featureSection" id="features">
        <div className="pageContainer">
          <div className="sectionIntro">
            <p className="eyebrow">ONE PLATFORM</p>
            <h2>Everything you need to stay informed.</h2>
            <p>Simple tools for users, clear controls for administrators.</p>
          </div>
          <div className="featureGrid">
            <article>
              <span className="featureNumber">01</span>
              <h3>Market visibility</h3>
              <p>Review the latest simulated market snapshot in a clean, readable view.</p>
            </article>
            <article>
              <span className="featureNumber">02</span>
              <h3>Secure accounts</h3>
              <p>Sign in with role-aware access and manage your personal account details.</p>
            </article>
            <article>
              <span className="featureNumber">03</span>
              <h3>Admin control</h3>
              <p>Administrators can manage users and roles from a dedicated workspace.</p>
            </article>
          </div>
        </div>
      </section>

      <section className="platformBanner" id="platform">
        <div className="pageContainer platformBannerInner">
          <div>
            <p className="eyebrow">BUILT TO EVOLVE</p>
            <h2>A dependable foundation for a growing exchange platform.</h2>
          </div>
          <Link className="textLink" href="/login">
            Sign in to continue <span aria-hidden="true">→</span>
          </Link>
        </div>
      </section>

      <footer className="siteFooter">
        <div className="pageContainer">
          <span>© {new Date().getFullYear()} ABADAR</span>
          <span>Market simulation platform</span>
        </div>
      </footer>
    </main>
  );
}
