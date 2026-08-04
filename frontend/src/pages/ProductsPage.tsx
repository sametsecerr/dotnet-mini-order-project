import { useState } from 'react';
import { EmptyState, ErrorMessage, Loading } from '../components/Feedback';
import { useProducts } from '../hooks/useProducts';
import { formatCurrency } from '../lib/format';

export function ProductsPage() {
  const [search, setSearch] = useState('');
  const { products, isLoading, error } = useProducts(search);

  return (
    <section>
      <header className="page-header">
        <h1>Urunler</h1>
        <input
          type="search"
          className="search-input"
          placeholder="Urun ismi veya stok kodu ile ara..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label="Urun ara"
        />
      </header>

      {error && <ErrorMessage error={error} />}
      {isLoading && <Loading />}

      {!isLoading && !error && products.length === 0 && (
        <EmptyState label="Aramanizla eslesen urun bulunamadi." />
      )}

      {!isLoading && !error && products.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Stok Kodu</th>
              <th>Urun</th>
              <th className="numeric">Fiyat</th>
              <th className="numeric">Stok</th>
            </tr>
          </thead>
          <tbody>
            {products.map((product) => (
              <tr key={product.id}>
                <td><code>{product.stockCode}</code></td>
                <td>{product.name}</td>
                <td className="numeric">{formatCurrency(product.price)}</td>
                <td className="numeric">
                  {product.stockQuantity === 0 ? (
                    <span className="badge badge--danger">Stokta yok</span>
                  ) : (
                    product.stockQuantity
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
