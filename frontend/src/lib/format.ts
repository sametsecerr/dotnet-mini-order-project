const currencyFormatter = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  minimumFractionDigits: 2,
});

const dateFormatter = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

export const formatCurrency = (value: number) => currencyFormatter.format(value);

/** API tarihleri UTC olarak döner; sunucu "Z" eklemediğinde de UTC kabul edilir. */
export const formatDateTime = (isoUtc: string) => {
  const normalized = isoUtc.endsWith('Z') ? isoUtc : `${isoUtc}Z`;
  return dateFormatter.format(new Date(normalized));
};
