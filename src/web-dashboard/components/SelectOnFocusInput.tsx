"use client";

/**
 * Input somente leitura que seleciona o próprio texto ao receber foco (conveniência pra
 * copiar a api_key em /settings). Precisa ser um Client Component: um Server Component não
 * pode passar uma função (onFocus) como prop pra um elemento DOM — só client-side.
 */
export function SelectOnFocusInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} onFocus={(e) => e.currentTarget.select()} />;
}
