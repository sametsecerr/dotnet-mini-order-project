import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, toError, type OrderDetail } from '../api';
import { ErrorMessage } from '../ErrorMessage';
import { TableSkeleton } from '../TableSkeleton';
import { formatCurrency, formatDateTime } from '../format';

export function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [order, setOrder] = useState<OrderDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let isActive = true;
    setIsLoading(true);
    setError(null);

    api
      .getOrder(Number(id))
      .then((data) => isActive && setOrder(data))
      .catch((err) => isActive && setError(toError(err)))
      .finally(() => isActive && setIsLoading(false));

    return () => {
      isActive = false;
    };
  }, [id]);

  if (error) return <ErrorMessage error={error} />;

  return (
    <section>
      <header className="page-header">
        <h1>
          Siparis <span>#{id}</span>
        </h1>
        <Link to="/orders" className="back-link">
          &larr; Siparis listesi
        </Link>
      </header>

      <dl className="detail-grid">
        <div>
          <dt>Musteri</dt>
          <dd>{order?.customerName ?? <span className="skeleton" style={{ width: '70%' }} />}</dd>
        </div>
        <div>
          <dt>Tarih</dt>
          <dd>
            {order ? formatDateTime(order.createdAtUtc) : <span className="skeleton" style={{ width: '70%' }} />}
          </dd>
        </div>
        <div>
          <dt>Fiyatlandirma</dt>
          <dd>{order?.pricingType ?? <span className="skeleton" style={{ width: '50%' }} />}</dd>
        </div>
        <div>
          <dt>Toplam</dt>
          <dd>
            {order ? formatCurrency(order.totalAmount) : <span className="skeleton" style={{ width: '60%' }} />}
          </dd>
        </div>
      </dl>

      <table>
        <thead>
          <tr>
            <th>Stok kodu</th>
            <th>Urun</th>
            <th className="numeric">Birim fiyat</th>
            <th className="numeric">Miktar</th>
            <th className="numeric">Satir tutari</th>
          </tr>
        </thead>

        {isLoading || !order ? (
          <TableSkeleton columns={5} rows={3} />
        ) : (
          <>
            <tbody>
              {order.items.map((item) => (
                <tr key={item.productId}>
                  <td><code>{item.stockCode}</code></td>
                  <td>{item.productName}</td>
                  <td className="numeric">{formatCurrency(item.unitPrice)}</td>
                  <td className="numeric">{item.quantity}</td>
                  <td className="numeric">{formatCurrency(item.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={4}>Toplam</td>
                <td className="numeric">{formatCurrency(order.totalAmount)}</td>
              </tr>
            </tfoot>
          </>
        )}
      </table>

      <p className="hint">
        Birim fiyatlar siparis anindaki degerlerdir. Urun fiyati sonradan degisse bile bu siparisin
        tutari degismez.
      </p>
    </section>
  );
}
