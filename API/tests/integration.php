<?php
declare(strict_types=1);
if (PHP_SAPI !== 'cli') { http_response_code(404); exit; }
$root = dirname(__DIR__);
$stage = sys_get_temp_dir() . '/pyroduck-api-test-' . bin2hex(random_bytes(8));
mkdir($stage, 0700);
$server = null;
$checks = 0;
$cookies = [];
function check(bool $ok, string $label): void {
    global $checks;
    if (!$ok) throw new RuntimeException($label);
    $checks++;
}
function request(string $path, ?array $form = null): array {
    global $base, $cookies;
    $headers = 'Cookie: ' . implode('; ', $cookies) . "\r\n";
    if ($form !== null) $headers .= "Content-Type: application/x-www-form-urlencoded\r\n";
    $context = stream_context_create(['http'=>['method'=>$form === null ? 'GET' : 'POST', 'header'=>$headers,
        'content'=>$form === null ? '' : http_build_query($form), 'ignore_errors'=>true, 'follow_location'=>0, 'timeout'=>5]]);
    $body = file_get_contents($base . $path, false, $context);
    $responseHeaders = $http_response_header ?? [];
    preg_match('/\s(\d{3})\s/', $responseHeaders[0] ?? '', $status);
    foreach ($responseHeaders as $header) if (preg_match('/^Set-Cookie:\s*([^;]+)/i', $header, $match)) {
        $name = explode('=', $match[1], 2)[0]; $cookies[$name] = $match[1];
    }
    return [(int)($status[1] ?? 0), (string)$body, $responseHeaders];
}
function csrf(string $html): string {
    if (!preg_match('/name="csrf" value="([a-f0-9]+)"/', $html, $match)) throw new RuntimeException('Missing CSRF token');
    return $match[1];
}
function remove_stage(string $directory): void {
    foreach (new FilesystemIterator($directory) as $file) {
        if ($file->isDir() && !$file->isLink()) remove_stage($file->getPathname()); else unlink($file->getPathname());
    }
    rmdir($directory);
}
try {
    foreach (['bootstrap.php','config.php','admin.php','campaigns.php','setup.php','campaigns.example.json'] as $file) copy($root . '/' . $file, $stage . '/' . $file);
    // Exercise real CLI setup with a disposable password, never a production credential.
    $setup = proc_open([PHP_BINARY, $stage . '/setup.php'], [['pipe','r'], ['pipe','w'], ['pipe','w']], $pipes, $stage);
    fwrite($pipes[0], "test-admin\nDisposable-Test-Password-2026\n"); fclose($pipes[0]);
    stream_get_contents($pipes[1]); fclose($pipes[1]); stream_get_contents($pipes[2]); fclose($pipes[2]);
    check(proc_close($setup) === 0, 'CLI setup');
    define('PYRODUCK_API', true);
    $local = require $stage . '/config.local.php';
    check(password_verify('Disposable-Test-Password-2026', $local['admin_password_hash']), 'Setup password hashing');
    $local['require_https'] = false; // Test-only; original production config is untouched.
    file_put_contents($stage . '/config.local.php', '<?php return ' . var_export($local, true) . ';');
    $socket = stream_socket_server('tcp://127.0.0.1:0', $errno, $error);
    if (!$socket) throw new RuntimeException($error);
    $address = stream_socket_get_name($socket, false); fclose($socket);
    $base = 'http://' . $address;
    $server = proc_open([PHP_BINARY, '-d', 'opcache.enable=0', '-S', $address, '-t', $stage], [['pipe','r'], ['file', $stage . '/server.log','a'], ['file', $stage . '/server.log','a']], $pipes, $stage);
    if (!is_resource($server)) throw new RuntimeException('Cannot start test server');
    fclose($pipes[0]);
    $ready = false;
    for ($i=0; $i<50; $i++) {
        $probe = @stream_socket_client('tcp://' . $address, $errno, $error, 0.1);
        if ($probe) { fclose($probe); $ready = true; break; }
        usleep(100000);
    }
    check($ready, 'Server started');
    [$status,$body] = request('/campaigns.php');
    $catalog = json_decode($body, true, 32, JSON_THROW_ON_ERROR);
    check($status === 200 && count($catalog['campaigns']) === 1 && $catalog['schemaVersion'] === 1, 'Unity-compatible default response');
    check($catalog['campaigns'][0]['productId'] === 'eggheads-full', 'Advertised product identity survives public response');
    check(json_decode(request('/campaigns.php?product=eggheads-full')[1], true)['campaigns'] === [], 'Host product does not advertise itself');
    check($catalog['campaigns'][0]['discountPercent'] === 0 && str_contains($catalog['campaigns'][0]['imageUrl'], '/API/images/'), 'Default artwork URL and no sale');
    check(request('/campaigns.php?product[]=x')[0] === 400, 'Array query rejected');
    check(request('/campaigns.php', ['anything'=>'x'])[0] === 405, 'API writes rejected');
    check(request('/config.php')[0] === 404 && request('/bootstrap.php')[0] === 404 && request('/setup.php')[0] === 404, 'Internal routes protected without htaccess');
    [$status,$html] = request('/admin.php');
    $token = csrf($html);
    check($status === 200 && !str_contains($html, '<textarea'), 'Anonymous admin cannot edit');
    check(request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>'{}'])[0] === 403, 'Anonymous save denied');
    check(request('/admin.php', ['action'=>'login','csrf'=>'wrong','username'=>'test-admin','password'=>'Disposable-Test-Password-2026'])[0] === 403, 'CSRF denied');
    check(request('/admin.php', ['action'=>'login','csrf'=>$token,'username'=>'test-admin','password'=>'incorrect-password'])[0] === 401, 'Incorrect password denied');
    [$status,$html,$headers] = request('/admin.php', ['action'=>'login','csrf'=>$token,'username'=>'test-admin','password'=>'Disposable-Test-Password-2026']);
    $token = csrf($html);
    check($status === 200 && str_contains($html, '<textarea'), 'Successful login');
    check(str_contains(implode('\n', $headers), 'HttpOnly') && str_contains(implode('\n', $headers), 'SameSite=Strict'), 'Session cookie flags');
    check(request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>'{broken'])[0] === 422, 'Malformed JSON rejected');
    $invalid = $catalog; $invalid['campaigns'][0]['targetUrl'] = 'https://evil.example/buy';
    check(request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>json_encode($invalid)])[0] === 422, 'Unapproved destination rejected');
    $invalid = $catalog; $invalid['campaigns'][0]['startUtc'] = '2026-02-30T00:00:00Z';
    check(request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>json_encode($invalid)])[0] === 422, 'Invalid date rejected');
    $targeted = $catalog;
    $invalid = $catalog; $invalid['campaigns'][0]['productId'] = ['eggheads-full'];
    check(request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>json_encode($invalid)])[0] === 422, 'Invalid product identity rejected');
    $targeted['campaigns'][0]['products'] = ['eggheads-lite'];
    $targeted['campaigns'][0]['languages'] = ['tr'];
    $targeted['campaigns'][0]['minVersion'] = '1.0';
    $targeted['campaigns'][0]['maxVersion'] = '2.0';
    check(request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>json_encode($targeted)])[0] === 200, 'Authenticated save');
    $match = json_decode(request('/campaigns.php?product=eggheads-lite&lang=tr&version=1.0')[1], true);
    check(count($match['campaigns']) === 1 && !isset($match['campaigns'][0]['products']), 'Inclusive version targeting and stripped metadata');
    $mismatch = json_decode(request('/campaigns.php?product=eggheads-lite&lang=en&version=1.0')[1], true);
    check(count($mismatch['campaigns']) === 0, 'Language mismatch');
    check(count(json_decode(request('/campaigns.php')[1], true)['campaigns']) === 0, 'Missing targeting parameters');
    check(request('/private/catalog.php')[0] === 404, 'Storage cannot be downloaded');
    $catalog['enabled'] = false;
    request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>json_encode($catalog)]);
    check(json_decode(request('/campaigns.php')[1], true)['enabled'] === false, 'Global kill switch');
    $catalog['enabled'] = true; $catalog['campaigns'] = [];
    request('/admin.php', ['action'=>'save','csrf'=>$token,'catalog'=>json_encode($catalog)]);
    check(json_decode(request('/campaigns.php')[1], true)['campaigns'] === [], 'Empty catalog preserved');
    check(request('/admin.php', ['action'=>'logout','csrf'=>$token])[0] === 303, 'Logout');
    check(!str_contains(request('/admin.php')[1], '<textarea'), 'Logout removes access');
    $rateStatus = 0;
    for ($i=0; $i<121; $i++) { $rateStatus = request('/campaigns.php')[0]; if ($rateStatus === 429) break; }
    check($rateStatus === 429, 'Public rate limiting');
    $token = csrf(request('/admin.php')[1]);
    for ($i=0; $i<11; $i++) { $rateStatus = request('/admin.php', ['action'=>'login','csrf'=>$token,'username'=>'test-admin','password'=>'incorrect-password'])[0]; if ($rateStatus === 429) break; }
    check($rateStatus === 429, 'Login rate limiting');
    // Turn the production HTTPS requirement back on in the test copy.
    $local['require_https'] = true;
    file_put_contents($stage . '/config.local.php', '<?php return ' . var_export($local, true) . ';');
    check(request('/campaigns.php')[0] === 400, 'Plain HTTP rejected by production configuration');
    echo "PASS: $checks integration checks\n";
} catch (Throwable $error) {
    fwrite(STDERR, 'FAIL: ' . $error->getMessage() . "\n");
    $failed = true;
} finally {
    if (is_resource($server)) { proc_terminate($server); proc_close($server); }
    if (is_dir($stage) && str_starts_with(basename($stage), 'pyroduck-api-test-')) remove_stage($stage);
}
exit(!empty($failed) ? 1 : 0);
