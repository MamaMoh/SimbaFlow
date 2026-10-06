"use client";

import { AccessDenied } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { PageHeader } from "@/components/ui/page-header";
import { BotLinkCard } from "@/components/bot/bot-link-card";
import { IntakeDefaultsCard } from "@/components/settings/intake-defaults-card";
import { LogoUpload } from "@/components/branding/logo-upload";
import useSWR from "swr";

export default function SettingsPage() {
  const { hasPermission } = usePermissions();
  const canAdmin = hasPermission("system.admin");
  // settings.write is the permission this page is named after; gating only on system.admin shut
  // out the roles that exist to manage settings.
  const canManageSettings = canAdmin || hasPermission("settings.write");
  const canReadSettings = canManageSettings || hasPermission("settings.read");
  const canUseBot = hasPermission("bot.use") || canAdmin;

  const { data: branding, mutate: mutateBranding } = useSWR(
    canReadSettings ? "/api/proxy/branding/agency" : null,
    (url: string) => fetch(url).then((r) => r.json()),
    { revalidateOnFocus: false }
  );
  const agencyLogoPath: string | null = branding?.data?.logoPath ?? null;
  const agencyLetterheadPath: string | null = branding?.data?.letterheadPath ?? null;

  if (!canReadSettings && !canUseBot) {
    return <AccessDenied resource="settings" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Settings" />

      {canManageSettings ? (
        <div className="space-y-5 rounded-lg border bg-card p-4 shadow-sm lg:max-w-xl">
          <div>
            <h2 className="text-sm font-semibold">Branding</h2>
            <p className="mt-0.5 text-xs text-muted-foreground">
              PNG or JPEG, up to 2MB. Used when the candidate has no partner agency.
            </p>
          </div>
          <LogoUpload
            endpoint="/api/proxy/branding/agency/letterhead"
            logoPath={agencyLetterheadPath}
            onChange={() => mutateBranding()}
            label="Letterhead"
            hint="Printed across the top of CVs and visa forms."
          />
          <LogoUpload
            endpoint="/api/proxy/branding/agency/logo"
            logoPath={agencyLogoPath}
            onChange={() => mutateBranding()}
            label="Logo"
            hint="Used when no letterhead is uploaded."
          />
        </div>
      ) : null}

      {canAdmin ? <IntakeDefaultsCard /> : null}

      {canUseBot ? <BotLinkCard /> : null}
    </div>
  );
}
