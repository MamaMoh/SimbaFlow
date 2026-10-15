"use client";

import { useState } from "react";
import { AccessDenied } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { PageHeader } from "@/components/ui/page-header";
import { BotLinkCard } from "@/components/bot/bot-link-card";
import { IntakeDefaultsCard } from "@/components/settings/intake-defaults-card";
import { PortalAnswersCard } from "@/components/settings/portal-answers-card";
import { OfficesCard } from "@/components/settings/offices-card";
import { BrowserHelperCard } from "@/components/settings/browser-helper-card";
import { LogoUpload } from "@/components/branding/logo-upload";
import { useAgencyBranding } from "@/lib/api/branding";

/**
 * The agency's own settings.
 *
 * Two columns: the branding on the left, Telegram linking beside it. The page ran as one narrow
 * column down the left-hand side with the whole right-hand half empty, and the Telegram card —
 * the one thing on here every member of staff uses — was below the fold under it.
 */
export default function SettingsPage() {
  const { hasPermission } = usePermissions();
  const canAdmin = hasPermission("system.admin");
  // settings.write is the permission this page is named after; gating only on system.admin shut
  // out the roles that exist to manage settings.
  const canManageSettings = canAdmin || hasPermission("settings.write");
  const canReadSettings = canManageSettings || hasPermission("settings.read");
  const canUseBot = hasPermission("bot.use") || canAdmin;

  const { branding, error, mutate } = useAgencyBranding(canReadSettings);

  // What the last upload returned, shown straight away. The card used to re-request the branding
  // and draw whatever came back, so an upload that succeeded still read "No logo" whenever that
  // second request did not — which is the state somebody was looking at while holding a file
  // they had just watched upload.
  const [justUploaded, setJustUploaded] = useState<{
    letterhead?: string | null;
    logo?: string | null;
  }>({});

  const pathFor = (kind: "letterhead" | "logo") =>
    kind in justUploaded
      ? justUploaded[kind] ?? null
      : (kind === "letterhead" ? branding?.letterheadPath : branding?.logoPath) ?? null;

  const applied = (kind: "letterhead" | "logo") => (path: string | null) => {
    setJustUploaded((current) => ({ ...current, [kind]: path }));
    void mutate();
  };

  if (!canReadSettings && !canUseBot) {
    return <AccessDenied resource="settings" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Settings" />

      {/* Both cards the same height, and the two images side by side rather than stacked — the
          branding card was twice as tall as the one next to it, which left half the row empty. */}
      <div className="grid gap-6 lg:grid-cols-2">
        {canManageSettings ? (
          <div className="flex h-full flex-col gap-5 rounded-lg border bg-card p-5 shadow-sm">
            <div>
              <h2 className="text-sm font-semibold">Branding</h2>
              <p className="mt-0.5 text-xs text-muted-foreground">
                Printed on this agency&apos;s own documents. PNG or JPEG, up to 2MB.
              </p>
            </div>

            {error ? (
              <p className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                The current branding could not be loaded, so what is shown below may be out of
                date. Uploading still works.
              </p>
            ) : null}

            <div className="grid gap-5 sm:grid-cols-2">
              <LogoUpload
                endpoint="/api/proxy/branding/agency/letterhead"
                logoPath={pathFor("letterhead")}
                onChange={applied("letterhead")}
                label="Letterhead"
                hint="Across the top of CVs and visa forms."
                stacked
              />
              <LogoUpload
                endpoint="/api/proxy/branding/agency/logo"
                logoPath={pathFor("logo")}
                onChange={applied("logo")}
                label="Logo"
                hint="In the top bar, and on documents with no letterhead."
                stacked
              />
            </div>
          </div>
        ) : null}

        {canUseBot ? <BotLinkCard /> : null}
      </div>

      {/* settings.write, like the save behind it. Gated on system.admin, the agency owner — the
          person who holds settings.write to set these — never saw the card. */}
      {canManageSettings ? <IntakeDefaultsCard /> : null}

      {canReadSettings ? <OfficesCard canEdit={canManageSettings} /> : null}

      {/* Readable by anyone who can see settings: filling a form needs these, and only
          changing them is restricted. */}
      {canReadSettings ? <PortalAnswersCard canEdit={canManageSettings} /> : null}

      {/* Not gated on settings permissions: the people who fill these forms all day are the
          ones who need this, and installing it changes nothing about the agency. */}
      <BrowserHelperCard />
    </div>
  );
}
