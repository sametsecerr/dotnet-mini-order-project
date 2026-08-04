import { useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, toError, type OrderDetail, type Product } from '../api';
import { ErrorMessage } from '../ErrorMessage';
import { formatCurrency } from '../format';
import { useProducts } from '../useProducts';

export function NewOrderPage() {
  const [search, setSearch] = useState('');
  const { products, isLoading, error: productsError, reload: reloadProducts } = useProducts(search);

  const [customerName, setCustomerName] = useState('');
  const [pricingType, setPricingType] = useState('Standard');
  // Secilen urun, arama filtresi onu listeden cikarsa bile sepette kalmali:
  // bu yuzden miktarla birlikte urunun kendisini de tutuyoruz.
  const [selected, setSelected] = useState<Record<number, { product: Product; quantity: number }>>({});

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<Error | null>(null);
  const [createdOrder, setCreatedOrder] = useState<OrderDetail | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  // Listede gorunen urun daha guncel; gorunmuyorsa secim anindaki kopyasi kullanilir.
  const selectedItems = useMemo(
    () =>
      Object.values(selected).map((item) => ({
        product: products.find((p) => p.id === item.product.id) ?? item.product,
        quantity: item.quantity,
      })),
    [selected, products],
  );

  const estimatedTotal = useMemo(
    () => selectedItems.reduce((sum, item) => sum + item.product.price * item.quantity, 0),
    [selectedItems],
  );

  const toggleProduct = (product: Product, checked: boolean) => {
    setSelected((current) => {
      const next = { ...current };
      if (checked) {
        next[product.id] = { product, quantity: 1 };
      } else {
        delete next[product.id];
      }
      return next;
    });
  };

  const changeQuantity = (product: Product, rawValue: string) => {
    const quantity = Number.parseInt(rawValue, 10);
    setSelected((current) => ({
      ...current,
      [product.id]: { product, quantity: Number.isNaN(quantity) ? 0 : quantity },
    }));
  };

  const validate = (): string | null => {
    if (customerName.trim().length < 2) {
      return 'Musteri adi en az 2 karakter olmalidir.';
    }
    if (selectedItems.length === 0) {
      return 'Siparis icin en az bir urun secmelisiniz.';
    }
    if (selectedItems.some((item) => item.quantity <= 0)) {
      return 'Her secili urun icin miktar sifirdan buyuk olmalidir.';
    }

    const overStock = selectedItems.find((item) => item.quantity > item.product.stockQuantity);
    if (overStock) {
      return `${overStock.product.name} icin mevcut stok ${overStock.product.stockQuantity} adet.`;
    }

    return null;
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setCreatedOrder(null);
    setSubmitError(null);

    const validationError = validate();
    setValidationMessage(validationError);
    if (validationError) {
      return;
    }

    setIsSubmitting(true);
    try {
      const order = await api.createOrder({
        customerName: customerName.trim(),
        pricingType,
        items: selectedItems.map((item) => ({ productId: item.product.id, quantity: item.quantity })),
      });

      setCreatedOrder(order);
      setSelected({});
      setCustomerName('');
    } catch (err) {
      setSubmitError(toError(err));
    } finally {
      setIsSubmitting(false);
      reloadProducts();
    }
  };

  return (
    <section>
      <header className="page-header">
        <h1>Yeni Siparis</h1>
        <input
          type="search"
          className="search-input"
          placeholder="Urun ismi veya stok kodu ile ara..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label="Urun ara"
        />
      </header>

      {createdOrder && (
        <div className="state state--success" role="status">
          <strong>
            #{createdOrder.id} numarali siparis olusturuldu. Toplam: {formatCurrency(createdOrder.totalAmount)}
          </strong>
          <p>
            <Link to={`/orders/${createdOrder.id}`}>Siparis detayini goruntule</Link>
          </p>
        </div>
      )}

      {submitError && <ErrorMessage error={submitError} />}
      {validationMessage && (
        <div className="state state--error" role="alert">
          <strong>{validationMessage}</strong>
        </div>
      )}

      {/* noValidate: tarayicinin kendi validation'i (miktar > max) submit'i sessizce engelliyor. */}
      <form onSubmit={handleSubmit} noValidate>
        <div className="form-row">
          <label>
            Musteri adi
            <input
              type="text"
              value={customerName}
              onChange={(event) => setCustomerName(event.target.value)}
              placeholder="Ornek Musteri"
              maxLength={200}
            />
          </label>

          <label>
            Fiyatlandirma tipi
            <select value={pricingType} onChange={(event) => setPricingType(event.target.value)}>
              <option value="Standard">Standard</option>
              <option value="Bulk">Bulk</option>
            </select>
          </label>
        </div>

        {productsError && <ErrorMessage error={productsError} />}
        {isLoading && <p className="state state--loading">Yukleniyor...</p>}
        {!isLoading && !productsError && products.length === 0 && (
          <p className="state">Aramanizla eslesen urun bulunamadi.</p>
        )}

        {!isLoading && !productsError && products.length > 0 && (
          <table>
            <thead>
              <tr>
                <th className="checkbox-column">Sec</th>
                <th>Urun</th>
                <th className="numeric">Fiyat</th>
                <th className="numeric">Stok</th>
                <th className="numeric">Miktar</th>
              </tr>
            </thead>
            <tbody>
              {products.map((product) => {
                const isSelected = product.id in selected;
                const isOutOfStock = product.stockQuantity === 0;

                return (
                  <tr key={product.id} className={isSelected ? 'row--selected' : undefined}>
                    <td className="checkbox-column">
                      <input
                        type="checkbox"
                        checked={isSelected}
                        disabled={isOutOfStock}
                        onChange={(event) => toggleProduct(product, event.target.checked)}
                        aria-label={`${product.name} sec`}
                      />
                    </td>
                    <td>
                      {product.name} <code>{product.stockCode}</code>
                    </td>
                    <td className="numeric">{formatCurrency(product.price)}</td>
                    <td className="numeric">
                      {isOutOfStock ? <span className="badge badge--danger">Stokta yok</span> : product.stockQuantity}
                    </td>
                    <td className="numeric">
                      <input
                        type="number"
                        className="quantity-input"
                        min={1}
                        max={product.stockQuantity}
                        value={isSelected ? selected[product.id].quantity : ''}
                        disabled={!isSelected}
                        onChange={(event) => changeQuantity(product, event.target.value)}
                        aria-label={`${product.name} miktar`}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        <footer className="form-footer">
          <span>
            {selectedItems.length} urun secildi &middot; Tahmini toplam: <strong>{formatCurrency(estimatedTotal)}</strong>
          </span>
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Gonderiliyor...' : 'Siparisi olustur'}
          </button>
        </footer>
      </form>
    </section>
  );
}
