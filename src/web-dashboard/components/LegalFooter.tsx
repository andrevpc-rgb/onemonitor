import Link from "next/link";

export function LegalFooter() {
  return (
    <p className="text-center text-xs" style={{ color: "#898781" }}>
      <Link href="/privacy" className="underline">
        Privacidade
      </Link>
      {" · "}
      <Link href="/terms" className="underline">
        Termos de uso
      </Link>
    </p>
  );
}
