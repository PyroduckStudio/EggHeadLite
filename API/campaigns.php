<?php
declare(strict_types=1);
define('PYRODUCK_API', true);
require __DIR__ . '/bootstrap.php';
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
if (!in_array($_SERVER['REQUEST_METHOD'], ['GET','HEAD'], true)) {
    http_response_code(405); header('Allow: GET, HEAD'); echo '{"error":"Method not allowed"}'; exit;
}
$filters = [];
foreach (['product','lang','version'] as $key) {
    $value = $_GET[$key] ?? '';
    $pattern = $key === 'version' ? '/^\d{1,5}(?:\.\d{1,5}){0,3}$/D' : '/^[a-z0-9-]{1,40}$/D';
    if (!is_string($value) || ($value !== '' && !preg_match($pattern, $value))) {
        http_response_code(400); echo '{"error":"Invalid query parameter"}'; exit;
    }
    $filters[$key] = $value;
}
rate_limit('public', 120, 60);
$data = catalog();
$cards = [];
foreach ($data['campaigns'] as $card) {
    // product query identifies the host; productId identifies what is advertised.
    if ($filters['product'] !== '' && isset($card['productId']) &&
        strcasecmp($filters['product'], $card['productId']) === 0) continue;
    foreach (['products'=>'product', 'languages'=>'lang'] as $field=>$query) {
        if (!empty($card[$field]) && !in_array($filters[$query], $card[$field], true)) continue 2;
    }
    foreach (['minVersion'=>'>=', 'maxVersion'=>'<='] as $field=>$operator) {
        if (isset($card[$field]) && ($filters['version'] === '' || !version_compare($filters['version'], $card[$field], $operator))) continue 2;
    }
    // Only return fields supported by Unity. Keep future dates for its local scheduler.
    $cards[] = array_intersect_key($card, array_flip(['id','productId','enabled','active','priority','startUtc','endUtc','title','description','badge','discountPercent','imageUrl','targetUrl']));
}
$body = json_encode(['schemaVersion'=>1, 'enabled'=>$data['enabled'], 'campaigns'=>$cards], JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR);
header('Cache-Control: public, max-age=300');
if ($_SERVER['REQUEST_METHOD'] !== 'HEAD') echo $body;
