String formatStatus(String status) {
  return status
      .split('_')
      .map((word) => word.isNotEmpty
          ? '${word[0].toUpperCase()}${word.substring(1).toLowerCase()}'
          : '')
      .join(' ');
}

String formatPrice(double price) {
  if (price <= 0) return 'Pending Quote';
  return '\$${price.toStringAsFixed(2)}';
}

String formatTotal(double total) {
  if (total <= 0) return 'TBD';
  return '\$${total.toStringAsFixed(2)}';
}
