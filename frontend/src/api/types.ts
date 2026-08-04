export interface Product {
  id: number;
  stockCode: string;
  name: string;
  price: number;
  stockQuantity: number;
}

export interface OrderItem {
  productId: number;
  stockCode: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderSummary {
  id: number;
  customerName: string;
  pricingType: string;
  createdAtUtc: string;
  totalAmount: number;
  itemCount: number;
}

export interface OrderDetail extends Omit<OrderSummary, 'itemCount'> {
  items: OrderItem[];
}

export interface CreateOrderRequest {
  customerName: string;
  pricingType?: string;
  items: Array<{ productId: number; quantity: number }>;
}
