const currency = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  minimumFractionDigits: 2,
});

const dateTime = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' });

export const formatCurrency = (value: number) => currency.format(value);

export const formatDateTime = (isoUtc: string) =>
  dateTime.format(new Date(isoUtc.endsWith('Z') ? isoUtc : `${isoUtc}Z`));
