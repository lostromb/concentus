QUnit.module( 'General' );

var global = self;
QUnit.test( 'Basic health tests', 1, function ( assert ) {
	global.recorderWorkerConfig = {
		recorderSoftware: 'Foo Bar Baz Software Services AG'
	};

	var wave = new Wave();
	assert.strictEqual( wave.setMetaData().formatMetadata( function(){} ), wave, 'formatMetadata should not throw errors' );
} );