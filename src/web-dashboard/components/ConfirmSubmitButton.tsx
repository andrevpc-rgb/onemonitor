"use client";

/**
 * Botão de submit com confirmação do navegador antes de deixar o form seguir — usado para
 * ações irreversíveis (ver DeleteDeviceButton em /devices). Precisa ser Client Component:
 * um Server Component não pode passar um handler de evento (onClick) a um elemento DOM.
 */
export function ConfirmSubmitButton({
  confirmMessage,
  children,
  className,
  style,
}: {
  confirmMessage: string;
  children: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
}) {
  return (
    <button
      type="submit"
      className={className}
      style={style}
      onClick={(e) => {
        if (!window.confirm(confirmMessage)) {
          e.preventDefault();
        }
      }}
    >
      {children}
    </button>
  );
}
