"use client";

const PARTNERS = [
  { name: "Etenaa Resources Company", country: "KSA", phone: "+966500000001" },
  { name: "Samood Al-khaleej Recruitment", country: "Kuwait", phone: "+965500000002" },
  { name: "Golden Gate Partners", country: "UAE", phone: "+971500000003" },
  { name: "Amjad Khayat", country: "KSA", phone: "+966500000004" },
  { name: "Gulf Care Agency", country: "Bahrain", phone: "+973500000005" },
];

export default function PartnersPage() {
  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Partners</h1>
        <p className="text-sm text-muted-foreground">Overseas sponsors / partner agencies</p>
      </div>
      <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
            <tr>
              <th className="p-3">Name</th>
              <th className="p-3">Country</th>
              <th className="p-3">Phone</th>
            </tr>
          </thead>
          <tbody>
            {PARTNERS.map((p) => (
              <tr key={p.name} className="border-t">
                <td className="p-3 font-medium">{p.name}</td>
                <td className="p-3">{p.country}</td>
                <td className="p-3 font-mono text-xs">{p.phone}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
