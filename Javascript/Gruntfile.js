module.exports = function( grunt ) {
	'use strict';

	grunt.loadNpmTasks( 'grunt-contrib-jshint' );
	//grunt.loadNpmTasks( 'grunt-contrib-qunit' );
	//grunt.loadNpmTasks( 'grunt-jsduck' );

	grunt.initConfig( {
		jshint: {
			all: [ 'worker/EmsArgs.js', 'worker/EmsWorkerProxy.js', 'worker/OpusEncoder.js', 'test/*.js' ]
		},
		qunit: {
			all: [ 'test/index.html' ]
		},
		jsduck: {
			main: {
				// source paths with your code
				src: [
					'recorder.js',
					'worker/*.js'
				],

				// docs output dir
				dest: 'docs',

				// extra options
				options: {
					'builtin-classes': true,
					'title': 'Recorder.js API documentation',
					'message': 'Currently unstable and in development. Don\'t trust this docs, yet!',
					'warnings': [],
					'external': [ 'ArrayBuffer', 'Blob', 'DataView', 'Uint8Array' ]
				}
			}
		}
	} );

	grunt.registerTask( 'test', [ 'jshint' ] );
	grunt.registerTask( 'default', [ 'test' ] );
};
