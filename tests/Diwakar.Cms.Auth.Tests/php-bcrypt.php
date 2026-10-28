<?php
// Synthetic test vectors only; input is read from stdin, never command-line secrets.
$input = json_decode(stream_get_contents(STDIN), true, flags: JSON_THROW_ON_ERROR);
if ($input['action'] === 'hash') {
    echo password_hash($input['password'], PASSWORD_BCRYPT, ['cost' => 12]);
} elseif ($input['action'] === 'verify') {
    echo password_verify($input['password'], $input['hash']) ? 'yes' : 'no';
}
