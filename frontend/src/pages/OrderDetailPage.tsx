import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, toError } from '../api/client';
import type { OrderDetail } from '../api/types';
import { ErrorMessage, Loading } from '../components/Feedback';
import { formatCurrency, formatDateTime } from '../lib/format';

export function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [order, setOrder] = useState<OrderDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let isActive = true;
    setIsLoading(true);
    setError(null);

    (async () => {
      try {
        const data = await api.getOrder(Number(id));
        if (isActive) {
          setOrder(data);
        }
      } catch (err) {
        if (isActive) {
          setError(toError(err));
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    })();

    return () => {
      isActive = false;
    };
  }, [id]);

  if (isLoading) return <Loading label="Siparis yukleniyor..." />;
  if (error) return <ErrorMessage error={error} />;
  if (!order) return null;

  return (
    <section>
      <header className="page-header">
        <h1>Siparis #{order.id}</h1>
        <Link to="/orders">&larr; Siparis listesine don</Link>
      </header>

      <dl className="detail-grid">
        <div>
          <dt>Musteri</dt>
          <dd>{order.customerName}</dd>
        </div>
        <div>
          <dt>Tarih</dt>
          <dd>{formatDateTime(order.createdAtUtc)}</dd>
        </div>
        <div>
          <dt>Fiyatlandirma</dt>
          <dd>{order.pricingType}</dd>
        </div>
        <div>
          <dt>Toplam</dt>
          <dd>{formatCurrency(order.totalAmount)}</dd>
        </div>
      </dl>

      <table>
        <thead>
          <tr>
            <th>Stok Kodu</th>
            <th>Urun</th>
            <th className="numeric">Birim fiyat</th>
            <th className="numeric">Miktar</th>
            <th className="numeric">Satir tutari</th>
          </tr>
        </thead>
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
            <td className="numeric"><strong>{formatCurrency(order.totalAmount)}</strong></td>
          </tr>
        </tfoot>
      </table>

      <p className="hint">
        Birim fiyatlar siparis anindaki degerlerdir; urun fiyati sonradan degisse bile bu
        siparisin tutari degismez.
      </p>
    </section>
  );
}
